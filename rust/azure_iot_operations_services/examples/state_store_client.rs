// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_services::state_store::{self, SetOptions};
use env_logger::Builder;

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::max())
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

    tokio::task::spawn(state_store_operations(
        session.create_managed_client(),
        session.create_exit_handle(),
    ));

    session.run().await.unwrap();
}

async fn state_store_operations(client: SessionManagedClient, exit_handle: SessionExitHandle) {
    let state_store_key = b"someKey";
    let state_store_value = b"someValue";
    let timeout = Duration::from_secs(10);

    let state_store_client = state_store::Client::new(
        client,
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    let observe_response = state_store_client
        .observe(state_store_key.to_vec(), Duration::from_secs(10))
        .await;
    log::info!("Observe response: {:?}", observe_response);

    tokio::task::spawn({
        async move {
            if let Ok(mut response) = observe_response {
                while let Some((notification, _)) = response.response.recv_notification().await {
                    log::info!("Notification: {:?}", notification);
                }
                log::info!("Notification receiver closed");
            }
        }
    });

    let set_response = state_store_client
        .set(
            state_store_key.to_vec(),
            state_store_value.to_vec(),
            timeout,
            None,
            SetOptions {
                expires: Some(Duration::from_secs(60)),
                ..SetOptions::default()
            },
        )
        .await
        .unwrap();
    log::info!("Set response: {:?}", set_response);

    let get_response = state_store_client
        .get(state_store_key.to_vec(), timeout)
        .await
        .unwrap();
    log::info!("Get response: {:?}", get_response);

    let unobserve_response = state_store_client
        .unobserve(state_store_key.to_vec(), timeout)
        .await
        .unwrap();
    log::info!("Unobserve response: {:?}", unobserve_response);

    let delete_response = state_store_client
        .del(state_store_key.to_vec(), None, timeout)
        .await
        .unwrap();
    log::info!("Delete response: {:?}", delete_response);

    state_store_client.shutdown().await.unwrap();

    exit_handle.try_exit().await.unwrap();
}
