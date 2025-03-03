// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{sync::Arc, time::Duration};

use tokio::{sync::Mutex, time::sleep};

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::leased_lock::{self, AcquireAndUpdateKeyOption};
use azure_iot_operations_services::state_store::{self, SetCondition, SetOptions};
use env_logger::Builder;

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

    let (mut session1, exit_handle1, state_store_client_arc_mutex1, leased_lock_client1) =
        create_clients(client_id1, lock_name);

    let (mut session2, exit_handle2, state_store_client_arc_mutex2, leased_lock_client2) =
        create_clients(client_id2, lock_name);

    let client_2_task = tokio::task::spawn(async move {
        leased_lock_client_2_operations(
            state_store_client_arc_mutex2,
            leased_lock_client2,
            exit_handle2,
        )
        .await;
    });

    sleep(Duration::from_secs(1)).await; // Give client 2 chance to observe first.

    let client_1_task = tokio::task::spawn(async move {
        leased_lock_client_1_operations(
            lock_name,
            state_store_client_arc_mutex1,
            leased_lock_client1,
            exit_handle1,
        )
        .await;
    });

    // let _ = tokio::join!(
    //     session1.run(),
    //     session2.run(),
    // );
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
    Arc<Mutex<state_store::Client<SessionManagedClient>>>,
    leased_lock::Client<SessionManagedClient>,
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
        crate::state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    let state_store_client_arc_mutex = Arc::new(Mutex::new(state_store_client));

    let leased_lock_client = leased_lock::Client::new(
        state_store_client_arc_mutex.clone(),
        client_id.as_bytes().to_vec(),
        lock_name.as_bytes().to_vec(),
    )
    .unwrap();

    (
        session,
        exit_handle,
        state_store_client_arc_mutex,
        leased_lock_client,
    )
}

/// In the functions below we show different calls that an application could make
/// into the `leased_lock::Client`. Not necessarily an application would need to
/// make all these calls, but they do show all that can be done with this client.

/// In `leased_lock_client_1_operations` you will find the following examples:
/// 1. Perform a basic
async fn leased_lock_client_1_operations(
    lock_name: &str,
    state_store_client_arc_mutex: Arc<Mutex<state_store::Client<SessionManagedClient>>>,
    leased_lock_client: leased_lock::Client<SessionManagedClient>,
    exit_handle: SessionExitHandle,
) {
    let lock_expiry = Duration::from_secs(10);
    let request_timeout = Duration::from_secs(120);

    let shared_resource_key_name = b"someKey";
    let shared_resource_key_value1 = b"someValue1";
    let shared_resource_key_set_options = SetOptions {
        set_condition: SetCondition::Unconditional,
        expires: Some(Duration::from_secs(15)),
    };

    // Individual operations (acquire_lock, observe, get_holder_name, unobserve).
    let fencing_token = match leased_lock_client
        .acquire_lock(lock_expiry, request_timeout)
        .await
    {
        Ok(acquire_lock_response) => {
            if acquire_lock_response.response {
                log::info!("Lock acquired successfuly");
                acquire_lock_response.fencing_token
            } else {
                log::error!("Could not acquire lock {:?}", acquire_lock_response);
                return;
            }
        }
        Err(e) => {
            log::error!("Failed acquiring lock {:?}", e);
            return;
        }
    };

    // The purpose of the lock is to protect setting a shared key in the state store.
    let locked_state_store_client = state_store_client_arc_mutex.lock().await;

    match locked_state_store_client
        .set(
            shared_resource_key_name.to_vec(),
            shared_resource_key_value1.to_vec(),
            request_timeout,
            fencing_token,
            shared_resource_key_set_options.clone(),
        )
        .await
    {
        Ok(set_response) => {
            if set_response.response {
                log::info!("Key set successfuly");
            } else {
                log::error!("Could not set key {:?}", set_response);
                return;
            }
        }
        Err(e) => {
            log::error!("Failed setting key {:?}", e);
            return;
        }
    };
    drop(locked_state_store_client); // Important to release the local lock on `state_store_client`.

    get_lock_holder(
        &leased_lock_client,
        lock_name.as_bytes().to_vec(),
        request_timeout,
    )
    .await;

    match leased_lock_client.release_lock(request_timeout).await {
        Ok(release_lock_response) => {
            if release_lock_response.response == 1 {
                log::info!("Lock released successfuly");
            } else {
                log::error!("Could not release lock {:?}", release_lock_response);
                return;
            }
        }
        Err(e) => {
            log::error!("Failed releasing lock {:?}", e);
            return;
        }
    };

    let locked_state_store_client = state_store_client_arc_mutex.lock().await;
    locked_state_store_client.shutdown().await.unwrap();

    exit_handle.try_exit().await.unwrap();
}

/// In `leased_lock_client_2_operations` you will find the following examples:
/// 1. Perform a basic
async fn leased_lock_client_2_operations(
    state_store_client_arc_mutex: Arc<Mutex<state_store::Client<SessionManagedClient>>>,
    leased_lock_client: leased_lock::Client<SessionManagedClient>,
    exit_handle: SessionExitHandle,
) {
    let lock_expiry = Duration::from_secs(10);
    let request_timeout = Duration::from_secs(10);

    let shared_resource_key_name = b"someKey";
    let shared_resource_key_value1 = b"someValue1";
    let shared_resource_key_value2 = b"someValue2";
    let _shared_resource_key_set_options = SetOptions {
        // TODO: use in acquire_lock_and_update_value
        set_condition: SetCondition::Unconditional,
        expires: Some(Duration::from_secs(15)),
    };

    let mut received_lock_free_notification = false;
    while !received_lock_free_notification {
        let mut observe_response = leased_lock_client
            .observe_lock(request_timeout)
            .await
            .unwrap();

        loop {
            let Some((notification, _)) = observe_response.response.recv_notification().await
            else {
                log::warn!("Received None for lock notification. Client probably disconnected. observe_lock() must be called again.");
                break;
            };

            log::info!(
                "Client2 received lock notification: {:?}",
                notification.operation
            );

            if notification.operation == state_store::Operation::Del {
                received_lock_free_notification = true;
                break;
            }
        }
    }

    // acquire_lock_and_update_value, acquire_lock_and_update_value
    match leased_lock_client
        .acquire_lock_and_update_value(
            lock_expiry,
            request_timeout,
            shared_resource_key_name.to_vec(),
            &|key_current_value: Option<Vec<u8>>| {
                // Perform some check on `key_current_value`...
                if key_current_value.unwrap() == shared_resource_key_value1.to_vec() {
                    AcquireAndUpdateKeyOption::Update(shared_resource_key_value2.to_vec())
                } else {
                    AcquireAndUpdateKeyOption::DoNotUpdate
                    // Handle unexpected value...
                }
            },
        )
        .await
    {
        Ok(acquire_lock_and_update_value_result) => {
            if acquire_lock_and_update_value_result.response {
                log::info!("Key successfuly set");
            } else {
                log::error!(
                    "Could not set key {:?}",
                    acquire_lock_and_update_value_result
                );
                return;
            }
        }
        Err(e) => {
            log::error!("Failed setting key {:?}", e);
            return;
        }
    };

    // Perform any application-logic and when done, delete key...

    match leased_lock_client
        .acquire_lock_and_update_value(
            lock_expiry,
            request_timeout,
            shared_resource_key_name.to_vec(),
            &|_key_current_value| {
                // Perform some check on `key_current_value`...
                AcquireAndUpdateKeyOption::Delete
            },
        )
        .await
    {
        Ok(acquire_lock_and_update_value_result) => {
            if acquire_lock_and_update_value_result.response {
                log::info!("Key successfuly deleted");
            } else {
                log::error!(
                    "Could not delete key {:?}",
                    acquire_lock_and_update_value_result
                );
                return;
            }
        }
        Err(e) => {
            log::error!("Failed deleting key {:?}", e);
            return;
        }
    };

    let locked_state_store_client = state_store_client_arc_mutex.lock().await;
    locked_state_store_client.shutdown().await.unwrap();

    exit_handle.try_exit().await.unwrap();
}

async fn get_lock_holder(
    leased_lock_client: &azure_iot_operations_services::leased_lock::Client<SessionManagedClient>,
    lock_name: Vec<u8>,
    request_timeout: Duration,
) {
    match leased_lock_client
        .get_lock_holder(lock_name, request_timeout)
        .await
    {
        Ok(lock_holder_response) => match lock_holder_response.response {
            Some(holder_name) => {
                log::info!(
                    "Lock being held by {}",
                    String::from_utf8(holder_name).unwrap()
                );
            }
            None => {
                log::info!("Lock is currently free");
            }
        },
        Err(e) => {
            log::error!("Failed getting lock holder {:?}", e);
        }
    };
}
