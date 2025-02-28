// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{sync::Arc, time::Duration};

use tokio::sync::Mutex;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::leased_lock::{self};
use azure_iot_operations_services::state_store::{self, SetCondition, SetOptions};
use env_logger::Builder;

#[tokio::main(flavor = "current_thread")]
async fn main() {
    let client_id1 = "someClientId1";
    let lock_name = "someLock";

    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    let (mut session1, exit_handle1, state_store_client_arc_mutex1, leased_lock_client1) =
        create_clients(client_id1, lock_name);

    tokio::task::spawn(leased_lock_client_1_operations(
        lock_name,
        state_store_client_arc_mutex1,
        leased_lock_client1,
        exit_handle1,
    ));

    let _join_result = tokio::join!(session1.run());
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
    let request_timeout = Duration::from_secs(10);

    let shared_resource_key_name = b"someKey";
    let shared_resource_key_value1 = b"someValue1";
    let shared_resource_key_value2 = b"someValue2";
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
                acquire_lock_response.version // Fencing token.
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

    match leased_lock_client.observe_lock(request_timeout).await {
        Ok(_observe_lock_response) => {
            log::info!("Observe lock succeeded");
        }
        Err(e) => {
            log::error!("Failed observing lock {:?}", e);
            return;
        }
    };

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

    get_lock_holder(
        &leased_lock_client,
        lock_name.as_bytes().to_vec(),
        request_timeout,
    )
    .await;

    match leased_lock_client.unobserve_lock(request_timeout).await {
        Ok(unobserve_lock_response) => {
            if unobserve_lock_response.response {
                log::info!("Unobserve lock succeeded");
            } else {
                log::error!("Could not unobserve lock {:?}", unobserve_lock_response);
                return;
            }
        }
        Err(e) => {
            log::error!("Failed unobserving lock {:?}", e);
            return;
        }
    };

    // acquire_lock_and_update_value, acquire_lock_and_delete_value
    match leased_lock_client
        .acquire_lock_and_update_value(
            lock_expiry,
            request_timeout,
            shared_resource_key_name.to_vec(),
            shared_resource_key_value2.to_vec(),
            shared_resource_key_set_options,
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

    {
        let locked_state_store_client = state_store_client_arc_mutex.lock().await;

        match locked_state_store_client
            .get(shared_resource_key_name.to_vec(), request_timeout)
            .await
        {
            Ok(get_response) => match get_response.response {
                Some(get_value) => {
                    log::info!(
                        "Key value retrieved: {}",
                        String::from_utf8(get_value).unwrap()
                    );
                }
                None => {
                    log::error!("Could not get key {:?}", get_response);
                }
            },
            Err(get_error) => {
                log::error!("Failed getting key {:?}", get_error);
                return;
            }
        }

        // Enclosed to drop locked_state_store.
    }

    match leased_lock_client
        .acquire_lock_and_delete_value(
            lock_expiry,
            request_timeout,
            shared_resource_key_name.to_vec(),
        )
        .await
    {
        Ok(acquire_lock_and_delete_value_result) => {
            if acquire_lock_and_delete_value_result.response == 1 {
                log::info!("Key successfuly deleted");
            } else {
                log::error!(
                    "Could not delete key {:?}",
                    acquire_lock_and_delete_value_result
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
