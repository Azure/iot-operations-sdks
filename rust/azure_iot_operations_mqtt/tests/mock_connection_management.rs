// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    fs,
    num::{NonZeroU16, NonZeroU32},
    time::Duration,
};

use bytes::Bytes;

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    session::{Session, SessionOptionsBuilder},
    test_utils::{
        IncomingPacketsTx, InjectedPacketChannels, MockSatFile, MockServer, OutgoingPacketsRx,
    },
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

fn expected_connect_from_connection_settings(
    connection_settings: &azure_iot_operations_mqtt::MqttConnectionSettings,
) -> mqtt_proto::Connect<Bytes> {
    mqtt_proto::Connect {
        username: connection_settings
            .username()
            .as_ref()
            .map(|un| un.as_str().into()),
        password: connection_settings
            .password()
            .as_ref()
            .map(|pw| pw.as_bytes().into()),
        will: None,
        client_id: Some(connection_settings.client_id().as_str().into()),
        clean_start: connection_settings.clean_start(),
        keep_alive: mqtt_proto::KeepAlive::Duration(
            NonZeroU16::new(connection_settings.keep_alive().as_secs() as u16).unwrap(),
        ),
        other_properties: mqtt_proto::ConnectOtherProperties {
            session_expiry_interval: mqtt_proto::SessionExpiryInterval::Duration(
                u32::try_from(connection_settings.session_expiry().as_secs()).unwrap(),
            ),
            receive_maximum: NonZeroU16::new(connection_settings.receive_max()).unwrap(),
            maximum_packet_size: NonZeroU32::new(
                connection_settings.receive_packet_size_max().unwrap(),
            )
            .unwrap(),
            topic_alias_maximum: 0, // Default value, not settable in SessionOptions
            request_response_information: false, // Default value, not settable in SessionOptions
            request_problem_information: true, // Default value, not settable in SessionOptions
            user_properties: vec![
                ("metriccategory".into(), "aiosdk-rust".into()), // Set by Session
                                                                 // No other properties are settable
            ],
            authentication: connection_settings
                .sat_file()
                .as_ref()
                .map(|sat_file_path| {
                    let contents: &[u8] = &fs::read(sat_file_path).unwrap();
                    mqtt_proto::Authentication {
                        method: "K8S-SAT".into(),
                        data: Some(contents.into()),
                    }
                }),
        },
    }
}

fn session_end_disconnect() -> mqtt_proto::Disconnect<Bytes> {
    mqtt_proto::Disconnect {
        reason_code: mqtt_proto::DisconnectReasonCode::Normal,
        other_properties: mqtt_proto::DisconnectOtherProperties {
            session_expiry_interval: Some(mqtt_proto::SessionExpiryInterval::Duration(0)), // ends the session
            reason_string: None,
            user_properties: vec![],
            server_reference: None,
        },
    }
}

fn expected_reauth_from_sat_file(mock_sat_file: &MockSatFile) -> mqtt_proto::Auth<Bytes> {
    let contents: &[u8] = &fs::read(mock_sat_file.path_as_str()).unwrap();
    mqtt_proto::Auth {
        reason_code: mqtt_proto::AuthenticateReasonCode::ReAuthenticate,
        authentication: Some(mqtt_proto::Authentication {
            method: "K8S-SAT".into(),
            data: Some(contents.into()),
        }),
        reason_string: None,
        user_properties: vec![],
    }
}

#[tokio::test]
async fn connect_and_exit_standard_auth() {
    let (mock_server, injected_packet_channels) = setup_mock_server();
    // NOTE: Make sure to use non-default values for the fields specified to validate that the user-specified fields
    // are actually being used.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("test-connect-and-exit-standard-auth-client")
        .hostname("test-hostname")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(10))
        .receive_max(100u16)
        .receive_packet_size_max(1000)
        .session_expiry(Duration::from_secs(25))
        .connection_timeout(Duration::from_secs(15))
        .clean_start(true)
        .username("test-username".to_string())
        .password("test-password".to_string())
        .sat_file(None) // Standard auth
        // The rest of the fields of connection settings are transport related and not relevant
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings.clone())
        .injected_packet_channels(Some(injected_packet_channels))
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(
        connect,
        expected_connect_from_connection_settings(&connection_settings)
    );

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // End the session
    assert_eq!(exit_handle.try_exit(), Ok(()));

    // Validate that the DISCONNECT packet is sent and contains the expected values
    let disconnect = mock_server.expect_disconnect().await;
    assert_eq!(disconnect, session_end_disconnect());

    // Session was disconnected, and exited cleanly
    monitor.disconnected().await;
    assert!(run_f.await.unwrap().is_ok());
}

#[tokio::test]
#[ignore]     // TODO: Investigate why reauth doesn't trigger
async fn connect_reauth_and_exit_enhanced_sat_auth() {
    let (mock_server, injected_packet_channels) = setup_mock_server();
    let mock_sat_file = MockSatFile::new();
    // NOTE: Make sure to use non-default values for the fields specified to validate that the user-specified fields
    // are actually being used.
    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("test-connect-reauth-and-exit-standard-auth-client")
        .hostname("test-hostname")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(10))
        .receive_max(100u16)
        .receive_packet_size_max(1000)
        .session_expiry(Duration::from_secs(25))
        .connection_timeout(Duration::from_secs(15))
        .clean_start(true)
        .username("test-username".to_string())
        //.password("test-password".to_string())
        .sat_file(mock_sat_file.path_as_str().to_string()) // Enhanced auth with SAT file
        // The rest of the fields of connection settings are transport related and not relevant
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings.clone())
        .injected_packet_channels(Some(injected_packet_channels))
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(
        connect,
        expected_connect_from_connection_settings(&connection_settings)
    );

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Trigger reauthentication by updating the SAT file contents
    mock_sat_file.update_contents();
    
    // Validate that the AUTH packet is sent with the expected values
    let auth = mock_server.expect_auth_and_accept().await;
    assert_eq!(auth, expected_reauth_from_sat_file(&mock_sat_file));

    // End the session
    assert_eq!(exit_handle.try_exit(), Ok(()));

    // Validate that the DISCONNECT packet is sent and contains the expected values
    let disconnect = mock_server.expect_disconnect().await;
    assert_eq!(disconnect, session_end_disconnect());

    // Session was disconnected, and exited cleanly
    monitor.disconnected().await;
    assert!(run_f.await.unwrap().is_ok());
}
