// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::Duration;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::state_store::{self, SetOptions};
use env_logger::Builder;

#[tokio::main(flavor = "current_thread")]
async fn main() -> Result<(), Box<dyn std::error::Error>> {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Create a Session and exit handle
    let connection_settings = MqttConnectionSettingsBuilder::from_environment()?.build()?;
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()?;
    let session = Session::new(session_options)?;
    let exit_handle = session.create_exit_handle();

    // Create an ApplicationContext
    let application_context = ApplicationContextBuilder::default().build()?;

    // Create a State Store Client
    let state_store_client = state_store::Client::new(
        application_context,
        session.create_managed_client(),
        session.create_connection_monitor(),
        state_store::ClientOptionsBuilder::default().build()?,
    )?;

    // Run the Session and the State Store operations concurrently
    let results = tokio::join! {
        async {
            state_store_operations(state_store_client).await;
            exit(exit_handle).await;
        },
        session.run(),
    };
    results.1?;
    Ok(())
}

async fn state_store_operations(client: state_store::Client<SessionManagedClient>) {
    let state_store_key = b"someKey";
    let state_store_value = b"someValue";
    let timeout = Duration::from_secs(10);

    let observe_response = client
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

    match client
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
    {
        Ok(response) => log::info!("Set response: {:?}", response),
        Err(e) => log::error!("Set error: {:?}", e),
    }

    match client.get(state_store_key.to_vec(), timeout).await {
        Ok(response) => log::info!("Get response: {:?}", response),
        Err(e) => log::error!("Get error: {:?}", e),
    }

    match client.unobserve(state_store_key.to_vec(), timeout).await {
        Ok(response) => log::info!("Unobserve response: {:?}", response),
        Err(e) => log::error!("Unobserve error: {:?}", e),
    }

    match client.del(state_store_key.to_vec(), None, timeout).await {
        Ok(response) => log::info!("Delete response: {:?}", response),
        Err(e) => log::error!("Delete error: {:?}", e),
    }

    match client.shutdown().await {
        Ok(()) => log::info!("State Store client shutdown successfully"),
        Err(e) => log::error!("State Store client shutdown error: {:?}", e),
    }
}

// Exit the Session
async fn exit(exit_handle: SessionExitHandle) {
    log::info!("Exiting session");
    match exit_handle.try_exit().await {
        Ok(()) => log::error!("Session exited gracefully"),
        Err(e) => {
            log::error!("Graceful session exit failed: {e}");
            log::error!("Forcing session exit");
            exit_handle.exit_force().await;
        }
    }
}
