// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
use std::str;
use std::time::{Duration, Instant};

use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionConnectionMonitor, SessionExitHandle, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;

const CLIENT_ID: &str = "aio_example_client";
//const HOST: &str = "localhost";
const HOST: &str = "test.mosquitto.org";
const PORT: u16 = 1883;

#[tokio::main(flavor = "current_thread")]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    // Build the options and settings for the session.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id(CLIENT_ID)
        .host_name(HOST)
        .tcp_port(PORT)
        .use_tls(false)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    // Create a new session.
    let mut session = Session::new(session_options).unwrap();

    // Spawn tasks for sending and receiving messages using managed clients
    // created from the session.
    tokio::spawn(uptime_monitor(session.create_connection_monitor()));
    tokio::spawn(exit_after_duration(
        session.create_exit_handle(),
        Duration::from_secs(60),
    ));
    // Run the session. This blocks until the session is exited.
    session.run().await.unwrap();
    // Briefly block to ensure uptime has time to log disconnect.
    tokio::time::sleep(Duration::from_secs(1)).await;
}

/// Monitor uptime
async fn uptime_monitor(monitor: SessionConnectionMonitor) {
    let mut total_uptime = Duration::default();
    loop {
        log::info!("Waiting for connection...");
        monitor.connected().await;
        log::info!("Connected! Beginning uptime monitoring...");
        let connect_time = Instant::now();
        monitor.disconnected().await;
        let disconnect_time = Instant::now();
        let uptime = disconnect_time - connect_time;
        log::info!("Disconnected after {:?}", uptime);
        total_uptime += uptime;
        log::info!("Total uptime: {:?}", total_uptime);
    }
}

/// Exit session after specified time
async fn exit_after_duration(exit_handle: SessionExitHandle, duration: Duration) {
    tokio::time::sleep(duration).await;
    exit_handle.try_exit().await.unwrap();
}
