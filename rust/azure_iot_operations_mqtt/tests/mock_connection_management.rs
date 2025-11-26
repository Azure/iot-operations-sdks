// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{num::{NonZeroU16, NonZeroU32}, time::Duration};

use bytes::Bytes;

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    session::{Session, SessionOptionsBuilder},
    test_utils::{IncomingPacketsTx, OutgoingPacketsRx, InjectedPacketChannels, MockServer},
};
use azure_mqtt::mqtt_proto; 

fn setup_mock_server() -> (MockServer, InjectedPacketChannels) {
    let incoming_packets_tx = IncomingPacketsTx::default();
    let outgoing_packets_rx = OutgoingPacketsRx::default();
    let mock_server = MockServer::new(incoming_packets_tx.clone(), outgoing_packets_rx.clone());
    let injected_packet_channels = InjectedPacketChannels {
        incoming_packets_tx,
        outgoing_packets_rx,
    };
    (mock_server, injected_packet_channels)
}


#[tokio::test]
async fn connect_disconnect() {
    let (mock_server, injected_packet_channels) = setup_mock_server();

    // NOTE: Make sure to use non-default values for the fields specified to validate that the user-specified fields
    // are actually being used.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("test-connect-disconnect-client")
        .hostname("test-hostname")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(120))
        .receive_max(100u16)
        .receive_packet_size_max(1000)
        .session_expiry(Duration::from_secs(25))
        .connection_timeout(Duration::from_secs(15))
        .clean_start(true)
        .username("test-username".to_string())
        .password("test-password".to_string())
        // The rest of the fields of connection settings are transport related and not relevant
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings.clone())
        .injected_packet_channels(Some(injected_packet_channels))
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();

    // Start the session run loop
    tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(connect.username, Some(connection_settings.username().clone().unwrap().as_str().into()));
    assert_eq!(connect.password, Some(Bytes::from(connection_settings.password().clone().unwrap()).as_ref().into()));
    assert_eq!(connect.will, None);
    assert_eq!(connect.client_id, Some(connection_settings.client_id().as_str().into()));
    assert!(connect.clean_start);
    assert_eq!(connect.keep_alive, mqtt_proto::KeepAlive::Duration(NonZeroU16::new(120).unwrap()));

    let expected_properties: mqtt_proto::ConnectOtherProperties<Bytes> = mqtt_proto::ConnectOtherProperties {
        session_expiry_interval: mqtt_proto::SessionExpiryInterval::Duration(u32::try_from(connection_settings.session_expiry().as_secs()).unwrap()),
        receive_maximum: NonZeroU16::new(connection_settings.receive_max()).unwrap(),
        maximum_packet_size: NonZeroU32::new(connection_settings.receive_packet_size_max().unwrap()).unwrap(),
        topic_alias_maximum: 0,                 // Default value, not settable in SessionOptions
        request_response_information: false,    // Default value, not settable in SessionOptions
        request_problem_information: true,      // Default value, not settable in SessionOptions
        user_properties: vec![
            ("metriccategory".into(), "aiosdk-rust".into())     // Set by Session
            // No other properties are settable
        ],
        authentication: None,
    };
    assert_eq!(connect.other_properties, expected_properties);
}

#[tokio::test]
async fn connect_reauth_disconnect() {}
