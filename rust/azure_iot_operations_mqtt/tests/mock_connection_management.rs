// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::{
    num::{NonZeroU16, NonZeroU32},
    time::Duration,
};

use bytes::Bytes;
use futures::FutureExt;

use azure_iot_operations_mqtt::azure_mqtt::mqtt_proto;
use azure_iot_operations_mqtt::{
    aio::connection_settings::{MqttConnectionSettings, MqttConnectionSettingsBuilder},
    control_packet::AuthenticationInfo,
    error::{SessionErrorKind, SessionExitErrorKind},
    session::{Session, SessionOptionsBuilder},
    test_utils::{
        IncomingPacketsTx, InjectedPacketChannels, MockEnhancedAuthPolicy,
        MockEnhancedAuthPolicyController, MockReconnectPolicy, MockReconnectPolicyController,
        MockServer, OutgoingPacketsRx,
    },
};

fn quick_setup_standard_auth(
    client_id: &str,
) -> (
    MqttConnectionSettings,
    Session,
    MockServer,
    MockReconnectPolicyController,
) {
    let (mock_server, injected_packet_channels) = setup_mock_server();
    let connection_settings = connection_settings_builder_preset(client_id)
        .build()
        .unwrap();
    let (mock_reconnect_policy, mock_reconnect_policy_controller) = MockReconnectPolicy::new();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings.clone())
        .reconnect_policy(Box::new(mock_reconnect_policy))
        .injected_packet_channels(Some(injected_packet_channels))
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();
    (
        connection_settings,
        session,
        mock_server,
        mock_reconnect_policy_controller,
    )
}

fn quick_setup_enhanced_auth(
    client_id: &str,
) -> (
    MqttConnectionSettings,
    Session,
    MockServer,
    MockReconnectPolicyController,
    MockEnhancedAuthPolicyController,
) {
    let (mock_server, injected_packet_channels) = setup_mock_server();
    let (mock_reconnect_policy, mock_reconnect_policy_controller) = MockReconnectPolicy::new();
    let (mock_enhanced_auth_policy, mock_enhanced_auth_policy_controller) =
        MockEnhancedAuthPolicy::new();
    let connection_settings = connection_settings_builder_preset(client_id)
        .build()
        .unwrap();
    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings.clone())
        .enhanced_auth_policy(Some(Box::new(mock_enhanced_auth_policy)))
        .reconnect_policy(Box::new(mock_reconnect_policy))
        .injected_packet_channels(Some(injected_packet_channels))
        .build()
        .unwrap();
    let session = Session::new(session_options).unwrap();
    (
        connection_settings,
        session,
        mock_server,
        mock_reconnect_policy_controller,
        mock_enhanced_auth_policy_controller,
    )
}

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

fn connection_settings_builder_preset(client_id: &str) -> MqttConnectionSettingsBuilder {
    // NOTE: Make sure to use non-default values for the fields specified when possible to validate
    // that the user-specified fields are actually being used.
    MqttConnectionSettingsBuilder::default()
        .client_id(client_id)
        .hostname("test-hostname")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(30))
        .receive_max(20u16)
        .receive_packet_size_max(2048)
        .session_expiry(Duration::from_secs(60))
        .connection_timeout(Duration::from_secs(15))
        .clean_start(true)
        .username("test-username".to_string())
        .password("test-password".to_string())
    // The rest of the fields of connection settings are transport related and not relevant
}

fn expected_connect(
    connection_settings: &MqttConnectionSettings,
    enhanced_auth_policy_controller: Option<&MockEnhancedAuthPolicyController>,
    prev_connected: bool,
) -> mqtt_proto::Connect<Bytes> {
    let expected_clean_start = if prev_connected {
        false
    } else {
        connection_settings.clean_start()
    };
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
        clean_start: expected_clean_start,
        keep_alive: mqtt_proto::KeepAlive::Duration(
            NonZeroU16::new(u16::try_from(connection_settings.keep_alive().as_secs()).unwrap())
                .unwrap(),
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
            authentication: enhanced_auth_policy_controller.map(|controller| {
                AuthenticationInfo {
                    method: controller.method().to_string(),
                    data: controller.auth_info_data(),
                }
                .into()
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

fn expected_reauth(
    mock_eap_controller: &MockEnhancedAuthPolicyController,
) -> mqtt_proto::Auth<Bytes> {
    mqtt_proto::Auth {
        reason_code: mqtt_proto::AuthenticateReasonCode::ReAuthenticate,
        authentication: Some(
            AuthenticationInfo {
                method: mock_eap_controller.method().to_string(),
                data: mock_eap_controller.reauth_data(),
            }
            .into(),
        ), // Easier to do whole struct conversion than using mqtt_proto directly
        reason_string: None,
        user_properties: vec![],
    }
}

#[tokio::test]
async fn connect_and_exit_standard_auth() {
    let (connection_settings, session, mock_server, _) =
        quick_setup_standard_auth("test-connect-and-exit-standard-auth-client");
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(connect, expected_connect(&connection_settings, None, false));

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // End the session
    assert!(matches!(exit_handle.try_exit(), Ok(())));

    // Validate that the DISCONNECT packet is sent and contains the expected values
    let disconnect = mock_server.expect_disconnect().await;
    assert_eq!(disconnect, session_end_disconnect());

    // Session was disconnected, and exited cleanly
    monitor.disconnected().await;
    assert!(run_f.await.unwrap().is_ok());
}

#[tokio::test]
async fn connect_reauth_and_exit_enhanced_auth() {
    let (connection_settings, session, mock_server, _, mock_eap_controller) =
        quick_setup_enhanced_auth("test-connect-reauth-and-exit-enhanced-auth-client");
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(
        connect,
        expected_connect(&connection_settings, Some(&mock_eap_controller), false)
    );

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Trigger reauth
    mock_eap_controller.reauth_notify();

    // Validate that the AUTH packet is sent with the expected values
    let auth = mock_server.expect_auth_and_accept().await;
    assert_eq!(auth, expected_reauth(&mock_eap_controller));

    // End the session
    assert!(matches!(exit_handle.try_exit(), Ok(())));

    // Validate that the DISCONNECT packet is sent and contains the expected values
    let disconnect = mock_server.expect_disconnect().await;
    assert_eq!(disconnect, session_end_disconnect());

    // Session was disconnected, and exited cleanly
    monitor.disconnected().await;
    assert!(run_f.await.unwrap().is_ok());
}

#[tokio::test]
async fn connect_failure_rejected_reconnect() {
    let (connection_settings, session, mock_server, mock_rp_controller) =
        quick_setup_standard_auth("test-connection-loss-server-disconnect-reconnect-client");
    mock_rp_controller.manual_mode(true);
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Wait for the connection attempt
    let connack = mqtt_proto::ConnAck {
        reason_code: mqtt_proto::ConnectReasonCode::Refused(
            mqtt_proto::ConnectionRefusedReason::NotAuthorized,
        ),
        other_properties: mqtt_proto::ConnAckOtherProperties::default(),
    };
    let connect = mock_server.expect_connect().await;
    assert_eq!(connect, expected_connect(&connection_settings, None, false));

    // Set up the reconnect policy mock to respond to the failure with a reconnect
    mock_rp_controller.set_next_delay(Some(Duration::from_secs(3)));
    let connect_failure_f = mock_rp_controller.connect_failure_notified();
    mock_server.send_connack(connack.clone());

    // The reconnect policy is invoked indicating connection failure
    connect_failure_f.await;
    assert!(!monitor.is_connected());

    // Expect a reconnect attempt after the expected delay
    let start = std::time::Instant::now();
    let connect = mock_server.expect_connect().await;
    let elapsed = start.elapsed();
    assert_eq!(connect, expected_connect(&connection_settings, None, false));
    assert!(elapsed < Duration::from_secs(4));
    assert!(elapsed >= Duration::from_secs(3));

    // Set up the reconnect policy mock to respond to the failure by ending the Session
    mock_rp_controller.set_next_delay(None);
    let connect_failure_f = mock_rp_controller.connect_failure_notified();
    mock_server.send_connack(connack.clone());

    // The reconnect policy is invoked indicating connection failure
    connect_failure_f.await;
    assert!(!monitor.is_connected());

    // Session exits due to reconnect policy indicating no more reconnects
    let e = run_f.await.unwrap().unwrap_err();
    assert!(matches!(e.kind(), SessionErrorKind::ReconnectHalted));
}

// TODO: connection failure due to IO error, protocol error(s), timeouts

#[tokio::test]
async fn connection_loss_server_disconnect_reconnect() {
    let (connection_settings, session, mock_server, mock_rp_controller) =
        quick_setup_standard_auth("test-connection-loss-server-disconnect-reconnect-client");
    mock_rp_controller.manual_mode(true);
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(connect, expected_connect(&connection_settings, None, false));

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Set up the reconnect policy mock to respond to the connection loss with a reconnect
    mock_rp_controller.set_next_delay(Some(Duration::from_secs(3)));
    let disconnect = mqtt_proto::Disconnect {
        reason_code: mqtt_proto::DisconnectReasonCode::UnspecifiedError,
        other_properties: mqtt_proto::DisconnectOtherProperties::default(),
    };
    let connection_loss_f = mock_rp_controller.connection_loss_notified();
    mock_server.send_disconnect(disconnect.clone());

    // The reconnect policy is invoked indicating connection loss
    connection_loss_f.await;
    // The Session detects the disconnection
    monitor.disconnected().await;

    // Expect a reconnect attempt after the expected delay
    let start = std::time::Instant::now();
    let connect = mock_server.expect_connect().await;
    let elapsed = start.elapsed();
    assert_eq!(connect, expected_connect(&connection_settings, None, true));
    assert!(elapsed < Duration::from_secs(4));
    assert!(elapsed >= Duration::from_secs(3));

    // Accept the connection
    mock_server.send_connack(mqtt_proto::ConnAck {
        reason_code: mqtt_proto::ConnectReasonCode::Success {
            session_present: true,
        },
        other_properties: mqtt_proto::ConnAckOtherProperties::default(),
    });

    // Wait for connection to be re-established by Session in response to CONNACK
    monitor.connected().await;

    // Set up the reconnect policy mock to respond to the next connection loss by ending the Session
    mock_rp_controller.set_next_delay(None);
    let connection_loss_f = mock_rp_controller.connection_loss_notified();
    mock_server.send_disconnect(disconnect);

    // The reconnect policy is invoked indicating connection loss
    connection_loss_f.await;
    // The Session detects the disconnection
    monitor.disconnected().await;

    // Session exits due to reconnect policy indicating no more reconnects
    let e = run_f.await.unwrap().unwrap_err();
    assert!(matches!(e.kind(), SessionErrorKind::ReconnectHalted));
}

// TODO: disconnect with Ping timeout, IO error(s), protocol error(s)

#[tokio::test]
async fn try_exit_never_run() {
    let (_, session, _, _) = quick_setup_standard_auth("test-try-exit-never-run-client");
    let exit_handle = session.create_exit_handle();

    // Try exiting before connecting
    let e = exit_handle.try_exit().unwrap_err();
    assert!(matches!(e.kind(), SessionExitErrorKind::ServerUnavailable));
}

#[tokio::test]
async fn try_exit_while_connected() {
    let (connection_settings, session, mock_server, _) =
        quick_setup_standard_auth("test-try-exit-while-connected-client");
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(connect, expected_connect(&connection_settings, None, false));

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Try exiting while connected
    assert!(matches!(exit_handle.try_exit(), Ok(())));

    // Validate that the DISCONNECT packet is sent and contains the expected values
    let disconnect = mock_server.expect_disconnect().await;
    assert_eq!(disconnect, session_end_disconnect());

    // Session was disconnected, and exited cleanly
    monitor.disconnected().await;
    assert!(run_f.await.unwrap().is_ok());
}

#[tokio::test]
async fn try_exit_while_disconnected() {
    let (connection_settings, session, mock_server, _) =
        quick_setup_standard_auth("test-try-exit-while-disconnected-client");
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(connect, expected_connect(&connection_settings, None, false));

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Set up the reconnect policy mock to respond to the connection loss with a reconnect
    mock_server.send_disconnect(mqtt_proto::Disconnect {
        reason_code: mqtt_proto::DisconnectReasonCode::UnspecifiedError,
        other_properties: mqtt_proto::DisconnectOtherProperties::default(),
    });

    // Wait for disconnection to be detected
    monitor.disconnected().await;

    // Try exiting while disconnected
    let e = exit_handle.try_exit().unwrap_err();
    assert!(matches!(e.kind(), SessionExitErrorKind::ServerUnavailable));

    // Session is still running, did not exit
    assert!(run_f.now_or_never().is_none());
    mock_server.expect_no_packet();
}

#[tokio::test]
async fn force_exit_never_run() {
    let (_, session, mock_server, _) =
        quick_setup_standard_auth("test-force-exit-never-run-client");
    let exit_handle = session.create_exit_handle();

    // Force exit before connecting
    let graceful = exit_handle.force_exit();

    // Not a graceful exit because we were never connected.
    assert!(!graceful);
    mock_server.expect_no_packet();
    // NOTE: Because the Session never ran, there's no exit condition to check.
}

#[tokio::test]
async fn force_exit_while_connected() {
    let (connection_settings, session, mock_server, _) =
        quick_setup_standard_auth("test-force-exit-while-connected-client");
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(connect, expected_connect(&connection_settings, None, false));

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Force exit while connected
    let graceful = exit_handle.force_exit();

    // Graceful exit because we could successfully send a DISCONNECT
    assert!(graceful);
    let disconnect = mock_server.expect_disconnect().await;
    assert_eq!(disconnect, session_end_disconnect());
    monitor.disconnected().await;

    // Session was ended, and exited cleanly
    assert!(run_f.await.unwrap().is_ok());
}

#[tokio::test]
async fn force_exit_while_disconnected() {
    let (connection_settings, session, mock_server, _) =
        quick_setup_standard_auth("test-force-exit-while-disconnected-client");
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(connect, expected_connect(&connection_settings, None, false));

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Set up the reconnect policy mock to respond to the connection loss with a reconnect
    mock_server.send_disconnect(mqtt_proto::Disconnect {
        reason_code: mqtt_proto::DisconnectReasonCode::UnspecifiedError,
        other_properties: mqtt_proto::DisconnectOtherProperties::default(),
    });

    // Wait for disconnection to be detected
    monitor.disconnected().await;

    // Force exit while disconnected
    let graceful = exit_handle.force_exit();

    // Not a graceful exit because we were disconnected.
    // No DISCONNECT packet should be sent.
    assert!(!graceful);
    mock_server.expect_no_packet();

    // Session was exited forcibly
    let e = run_f.await.unwrap().unwrap_err();
    assert!(matches!(e.kind(), SessionErrorKind::ForceExit));
}

/// This test validates that a force exit can be done while a reauthentication is pending.
#[tokio::test]
async fn force_exit_during_reauth() {
    let (connection_settings, session, mock_server, _, mock_eap_controller) =
        quick_setup_enhanced_auth("test-force-exit-during-reauth-client");
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(
        connect,
        expected_connect(&connection_settings, Some(&mock_eap_controller), false)
    );

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Trigger reauthentication
    mock_eap_controller.reauth_notify();

    // Receive the AUTH packet on the server, but do not respond.
    let auth = mock_server.expect_auth().await;
    assert_eq!(auth, expected_reauth(&mock_eap_controller));

    // Force exit while reauth is pending (i.e. have not responded with the server)
    let graceful = exit_handle.force_exit();
    assert!(graceful);

    // Session was ended gracefully with DISCONNECT, and exited cleanly
    let disconnect = mock_server.expect_disconnect().await;
    assert_eq!(disconnect, session_end_disconnect());
    monitor.disconnected().await;
    assert!(run_f.await.unwrap().is_ok());

    // Triggering reauth after exit does not cause any AUTH packets to be sent
    mock_eap_controller.reauth_notify();
    tokio::time::sleep(Duration::from_secs(1)).await;
    mock_server.expect_no_packet();
}

/// This test exists to verify that the reauthentication task is properly cancelled on disconnect,
/// and does not persist across connections.
#[tokio::test] // NOTE: This test WILL take > 30 seconds.
async fn reauth_on_successive_connections() {
    let (connection_settings, session, mock_server, _, mock_eap_controller) =
        quick_setup_enhanced_auth("test-reauth-on-successive-connections-client");
    let exit_handle = session.create_exit_handle();
    let monitor = session.create_session_monitor();
    assert!(!monitor.is_connected());

    // Start the session run loop
    let run_f = tokio::task::spawn(session.run());

    // Validate that the CONNECT packet contains the expected values
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(
        connect,
        expected_connect(&connection_settings, Some(&mock_eap_controller), false)
    );

    // Wait for connection to be established by Session in response to CONNACK
    monitor.connected().await;

    // Trigger reauthentication
    mock_eap_controller.reauth_notify();

    // Validate that a single AUTH packet is sent with the expected values
    let auth = mock_server.expect_auth_and_accept().await;
    assert_eq!(auth, expected_reauth(&mock_eap_controller));
    tokio::time::sleep(Duration::from_secs(1)).await;
    mock_server.expect_no_packet();

    // Disconnect the session and wait for disconnect
    mock_server.send_disconnect(mqtt_proto::Disconnect {
        reason_code: mqtt_proto::DisconnectReasonCode::UnspecifiedError,
        other_properties: mqtt_proto::DisconnectOtherProperties::default(),
    });
    monitor.disconnected().await;

    // Reconnect
    let connect = mock_server.expect_connect_and_accept(true).await;
    assert_eq!(
        connect,
        expected_connect(&connection_settings, Some(&mock_eap_controller), true)
    );
    monitor.connected().await;

    // Trigger reauthentication
    mock_eap_controller.reauth_notify();

    // Validate that a single AUTH packet is sent with the expected values
    let auth = mock_server.expect_auth_and_accept().await;
    assert_eq!(auth, expected_reauth(&mock_eap_controller));
    tokio::time::sleep(Duration::from_secs(1)).await;
    mock_server.expect_no_packet();

    // End the session
    assert!(matches!(exit_handle.try_exit(), Ok(())));

    // Session was disconnected, and exited cleanly
    monitor.disconnected().await;
    assert!(run_f.await.unwrap().is_ok());

    // Validate that the DISCONNECT packet is sent and contains the expected values
    let disconnect = mock_server.expect_disconnect().await;
    assert_eq!(disconnect, session_end_disconnect());

    // Triggering reauth after exit does not cause any AUTH packets to be sent
    mock_eap_controller.reauth_notify();
    tokio::time::sleep(Duration::from_secs(1)).await;
    mock_server.expect_no_packet();
}
