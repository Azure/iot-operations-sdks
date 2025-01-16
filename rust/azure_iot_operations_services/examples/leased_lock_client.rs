// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_services::leased_lock::{self};
use env_logger::Builder;

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a session
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("someClientId")
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
    let mut session = Session::new(session_options).unwrap();

    tokio::task::spawn(leased_lock_operations(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    session.run().await.unwrap();
}

async fn leased_lock_operations(client: SessionManagedClient, exit_handle: SessionExitHandle) {
    let lock_holder = b"lockHolder";
    let lock_key = b"lockKey1";
    let lock_expiry = Duration::from_secs(10);
    let request_timeout = Duration::from_secs(10);

    let leased_lock_client = leased_lock::Client::new(client, lock_holder.to_vec()).unwrap();

    match leased_lock_client
        .acquire_lock(lock_key.to_vec(), lock_expiry, request_timeout)
        .await
    {
        Ok(acquire_lock_response) => {
            if acquire_lock_response.response {
                log::info!("Lock acquired successfuly");
            } else {
                log::error!("Failed acquiring lock {:?}", acquire_lock_response);
                return;
            }
        }
        Err(e) => {
            log::error!("Failed acquiring lock {:?}", e);
            return;
        }
    };

    match leased_lock_client
        .observe_lock(lock_key.to_vec(), request_timeout)
        .await
    {
        Ok(_observe_lock_response) => {
            log::info!("Observe lock succeeded");
        }
        Err(e) => {
            log::error!("Failed observing lock {:?}", e);
            return;
        }
    };

    get_lock_holder(&leased_lock_client, lock_key.to_vec(), request_timeout).await;

    match leased_lock_client
        .release_lock(lock_key.to_vec(), request_timeout)
        .await
    {
        Ok(release_lock_response) => {
            if release_lock_response.response == 1 {
                log::info!("Lock released successfuly");
            } else {
                log::error!("Failed releasing lock {:?}", release_lock_response);
                return;
            }
        }
        Err(e) => {
            log::error!("Failed releasing lock {:?}", e);
            return;
        }
    };

    get_lock_holder(&leased_lock_client, lock_key.to_vec(), request_timeout).await;

    match leased_lock_client
        .unobserve_lock(lock_key.to_vec(), request_timeout)
        .await
    {
        Ok(unobserve_lock_response) => {
            if unobserve_lock_response.response {
                log::info!("Unobserve lock succeeded");
            } else {
                log::error!("Failed unobserving lock {:?}", unobserve_lock_response);
                return;
            }
        }
        Err(e) => {
            log::error!("Failed unobserving lock {:?}", e);
            return;
        }
    };

    leased_lock_client.shutdown().await.unwrap();

    exit_handle.try_exit().await.unwrap();
}

async fn get_lock_holder(
    leased_lock_client: &azure_iot_operations_services::leased_lock::Client<SessionManagedClient>,
    lock_key: Vec<u8>,
    request_timeout: Duration,
) {
    match leased_lock_client
        .get_lock_holder(lock_key, request_timeout)
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
