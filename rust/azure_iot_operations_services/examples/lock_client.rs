// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/// This example shows the how to use `lock::Client` for locking/unlocking operations.
use std::{sync::Arc, time::Duration};

use env_logger::Builder;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::leased_lock::{SetCondition, SetOptions, lock};
use azure_iot_operations_services::state_store;

#[tokio::main(flavor = "current_thread")]
async fn main() {
    let client_id1 = "someClientId1";
    let client_id2 = "someClientId2";
    let lock_name = "someLock";

    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    let (session1, exit_handle1, state_store_client_arc1, lock_client1) =
        create_clients(client_id1, lock_name);

    let (session2, exit_handle2, state_store_client_arc2, lock_client2) =
        create_clients(client_id2, lock_name);

    let client_1_task = tokio::task::spawn(async move {
        lock_client_1_operations(state_store_client_arc1, lock_client1, exit_handle1).await;
    });

    let client_2_task = tokio::task::spawn(async move {
        lock_client_2_operations(state_store_client_arc2, lock_client2, exit_handle2).await;
    });

    let _ = tokio::try_join!(
        async move { client_1_task.await.map_err(|e| { e.to_string() }) },
        async move { session1.run().await.map_err(|e| { e.to_string() }) },
        async move { client_2_task.await.map_err(|e| { e.to_string() }) },
        async move { session2.run().await.map_err(|e| { e.to_string() }) },
    );
}

fn create_clients(
    client_id: &str,
    lock_name: &str,
) -> (
    Session,
    SessionExitHandle,
    Arc<state_store::Client<SessionManagedClient>>,
    lock::Client<SessionManagedClient>,
) {
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    // Create a session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();

    let exit_handle = session.create_exit_handle();

    let state_store_client = state_store::Client::new(
        application_context,
        session.create_managed_client(),
        session.create_connection_monitor(),
        crate::state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    let state_store_client_arc = Arc::new(state_store_client);

    let lock_client = lock::Client::new(
        state_store_client_arc.clone(),
        lock_name.as_bytes().to_vec(),
        client_id.as_bytes().to_vec(),
    )
    .unwrap();

    (session, exit_handle, state_store_client_arc, lock_client)
}

/// In the functions below we show different calls that an application could make
/// into the `leased_lock::Client`. Not necessarily an application would need to
/// make all these calls, but they do show all that can be done with this client.
///
/// In `lock_client_1_operations` you will find the following examples:
/// 1. Acquire a lock using `lock()`
/// 2. Sets a key in the State Store using the `fencing_token` obtained from the lock.
/// 3. Releases a lock.
async fn lock_client_1_operations(
    state_store_client_arc: Arc<state_store::Client<SessionManagedClient>>,
    lock_client: lock::Client<SessionManagedClient>,
    exit_handle: SessionExitHandle,
) {
    let lock_expiry = Duration::from_secs(10);
    let request_timeout = Duration::from_secs(120);

    let shared_resource_lock_name = b"someKey";
    let shared_resource_key_value1 = b"someValue1";
    let shared_resource_key_set_options = SetOptions {
        set_condition: SetCondition::Unconditional,
        expires: Some(Duration::from_secs(15)),
    };

    // 1.
    let fencing_token = match lock_client.lock(lock_expiry, request_timeout, None).await {
        Ok(acquire_result) => {
            log::info!("Lock acquired successfully");
            acquire_result // a.k.a., the fencing token.
        }
        Err(e) => {
            log::error!("Failed acquiring lock {e}");
            return;
        }
    };

    // The purpose of the lock is to protect setting a shared key in the state store.
    // 2.
    match state_store_client_arc
        .set(
            shared_resource_lock_name.to_vec(),
            shared_resource_key_value1.to_vec(),
            request_timeout,
            Some(fencing_token),
            shared_resource_key_set_options.clone(),
        )
        .await
    {
        Ok(set_response) => {
            if set_response.response {
                log::info!("Key set successfully");
            } else {
                log::error!("Could not set key {set_response:?}");
                return;
            }
        }
        Err(e) => {
            log::error!("Failed setting key {e}");
            return;
        }
    };

    // 3.

    // 4.
    match lock_client.unlock(request_timeout).await {
        Ok(()) => {
            log::info!("Lock released successfully");
        }
        Err(e) => {
            log::error!("Failed releasing lock {e}");
            return;
        }
    };

    state_store_client_arc.shutdown().await.unwrap();

    exit_handle.try_exit().await.unwrap();
}

/// In `lock_client_2_operations` you will find the following examples:
/// 4. Waits until lock the lock is acquired.
/// 5. Sets a key in the State Store using the `fencing_token` obtained from the lock.
/// 6. Releases the lock.
async fn lock_client_2_operations(
    state_store_client_arc: Arc<state_store::Client<SessionManagedClient>>,
    lock_client: lock::Client<SessionManagedClient>,
    exit_handle: SessionExitHandle,
) {
    let lock_expiry = Duration::from_secs(10);
    let request_timeout = Duration::from_secs(120);

    let shared_resource_lock_name = b"someKey";
    let shared_resource_key_value2 = b"someValue2";
    let shared_resource_key_set_options = SetOptions {
        set_condition: SetCondition::Unconditional,
        expires: Some(Duration::from_secs(15)),
    };

    // 4.
    let fencing_token = match lock_client.lock(lock_expiry, request_timeout, None).await {
        Ok(acquire_result) => {
            log::info!("Lock acquired successfully");
            acquire_result // a.k.a., the fencing token.
        }
        Err(e) => {
            log::error!("Failed acquiring lock {e}");
            return;
        }
    };

    // The purpose of the lock is to protect setting a shared key in the state store.
    // 5.
    match state_store_client_arc
        .set(
            shared_resource_lock_name.to_vec(),
            shared_resource_key_value2.to_vec(),
            request_timeout,
            Some(fencing_token),
            shared_resource_key_set_options.clone(),
        )
        .await
    {
        Ok(set_response) => {
            if set_response.response {
                log::info!("Key set successfully");
            } else {
                log::error!("Could not set key {set_response:?}");
                return;
            }
        }
        Err(e) => {
            log::error!("Failed setting key {e}");
            return;
        }
    };

    // 6.
    match lock_client.unlock(request_timeout).await {
        Ok(()) => {
            log::info!("Lock released successfully");
        }
        Err(e) => {
            log::error!("Failed releasing lock {e}");
            return;
        }
    };

    state_store_client_arc.shutdown().await.unwrap();

    exit_handle.try_exit().await.unwrap();
}
