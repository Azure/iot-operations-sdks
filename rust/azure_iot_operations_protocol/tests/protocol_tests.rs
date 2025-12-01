// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

mod metl;

use std::path::Path;
use std::sync::atomic;

use azure_iot_operations_mqtt::{
    MqttConnectionSettingsBuilder,
    session::{Session, SessionOptionsBuilder},
    test_utils::{IncomingPacketsTx, InjectedPacketChannels, OutgoingPacketsRx},
};
use tokio::runtime::Builder;

use metl::command_executor_tester::CommandExecutorTester;
use metl::command_invoker_tester::CommandInvokerTester;
use metl::defaults::{
    DefaultsType, ExecutorDefaults, InvokerDefaults, ReceiverDefaults, SenderDefaults,
};
use metl::mqtt_emulation_level::MqttEmulationLevel;
use metl::mqtt_hub::MqttHub;
use metl::telemetry_receiver_tester::TelemetryReceiverTester;
use metl::telemetry_sender_tester::TelemetrySenderTester;
use metl::test_case::TestCase;
use metl::test_feature_kind::TestFeatureKind;

static TEST_CASE_INDEX: atomic::AtomicI32 = atomic::AtomicI32::new(0);

const PROBLEMATIC_TEST_CASES: &[&str] = &[
    "CommandExecutorRequestExpiresWhileDisconnected_RequestNotAcknowledged",
    "CommandExecutorResponsePubAckDroppedByDisconnection_ReconnectAndSuccess",
    "CommandInvokerInvalidResponseTopicPrefix_ThrowsException",
    "CommandInvokerInvalidResponseTopicSuffix_ThrowsException",
    "CommandInvokerWithZeroTimeout_ThrowsException",
    "TelemetrySenderPubAckDroppedByDisconnection_ReconnectAndSuccess", // this might be able to be tested once acks have the epoch
    "TelemetrySenderSendWithCloudEventSpecVersionNonNumeric_Success",
    "CommandExecutorValidTopicNamespaceWithTopicTokens_Success",
    "TelemetryReceiverWithTopicNamespaceAndTopicTokens_Success",
];

/*
#[allow(clippy::unnecessary_wraps)]
#[allow(clippy::needless_pass_by_value)]
fn test_command_invoker_standalone(_path: &Path, contents: String) -> datatest_stable::Result<()> {
    let wrapped_test_case: serde_yaml::Result<TestCase<InvokerDefaults>> =
        serde_yaml::from_str(contents.as_str());

    assert!(wrapped_test_case.is_ok());
    let test_case = wrapped_test_case.unwrap();

    let test_case_index = TEST_CASE_INDEX.fetch_add(1, atomic::Ordering::Relaxed);

    if !PROBLEMATIC_TEST_CASES.contains(&test_case.test_name.as_str())
        && does_standalone_support(&test_case.requires)
    {
        let mqtt_client_id =
            get_client_id(&test_case, "StandaloneInvokerTestClient", test_case_index);
        let mqtt_hub = MqttHub::new(mqtt_client_id, MqttEmulationLevel::Message);
        let mqtt_driver = mqtt_hub.get_driver();

        Builder::new_current_thread()
            .enable_all()
            .build()
            .unwrap()
            .block_on(CommandInvokerTester::<MqttDriver>::test_command_invoker(
                test_case,
                mqtt_driver,
                mqtt_hub,
            ));
    }

    Ok(())
}
*/

/*
#[allow(clippy::unnecessary_wraps)]
#[allow(clippy::needless_pass_by_value)]
fn test_command_executor_standalone(_path: &Path, contents: String) -> datatest_stable::Result<()> {
    let wrapped_test_case: serde_yaml::Result<TestCase<ExecutorDefaults>> =
        serde_yaml::from_str(contents.as_str());

    assert!(wrapped_test_case.is_ok());
    let test_case = wrapped_test_case.unwrap();

    let test_case_index = TEST_CASE_INDEX.fetch_add(1, atomic::Ordering::Relaxed);

    if !PROBLEMATIC_TEST_CASES.contains(&test_case.test_name.as_str())
        && does_standalone_support(&test_case.requires)
    {
        let mqtt_client_id =
            get_client_id(&test_case, "StandaloneExecutorTestClient", test_case_index);
        let mqtt_hub = MqttHub::new(mqtt_client_id, MqttEmulationLevel::Message);
        let mqtt_driver = mqtt_hub.get_driver();

        Builder::new_current_thread()
            .enable_all()
            .build()
            .unwrap()
            .block_on(CommandExecutorTester::<MqttDriver>::test_command_executor(
                test_case,
                mqtt_driver,
                mqtt_hub,
            ));
    }

    Ok(())
}
*/

#[allow(clippy::unnecessary_wraps)]
#[allow(clippy::needless_pass_by_value)]
fn test_command_invoker_session(_path: &Path, contents: String) -> datatest_stable::Result<()> {
    let wrapped_test_case: serde_yaml::Result<TestCase<InvokerDefaults>> =
        serde_yaml::from_str(contents.as_str());

    assert!(wrapped_test_case.is_ok());
    let test_case = wrapped_test_case.unwrap();

    let test_case_index = TEST_CASE_INDEX.fetch_add(1, atomic::Ordering::Relaxed);

    if !PROBLEMATIC_TEST_CASES.contains(&test_case.test_name.as_str())
        && does_session_support(&test_case.requires)
    {
        let mqtt_client_id = get_client_id(&test_case, "SessionInvokerTestClient", test_case_index);
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id(mqtt_client_id)
            .hostname("localhost")
            .tcp_port(1883u16)
            .use_tls(false)
            .build()?;
        let incoming_packets_tx = IncomingPacketsTx::default();
        let outgoing_packets_rx = OutgoingPacketsRx::default();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .injected_packet_channels(Some(InjectedPacketChannels {
                incoming_packets_tx: incoming_packets_tx.clone(),
                outgoing_packets_rx: outgoing_packets_rx.clone(),
            }))
            .build()?;
        let session = Session::new(session_options).unwrap();
        let mqtt_hub = MqttHub::new(
            MqttEmulationLevel::Event,
            incoming_packets_tx,
            outgoing_packets_rx,
        );
        let managed_client = session.create_managed_client();

        let current_thread = Builder::new_current_thread().enable_all().build().unwrap();

        let exit_handle = session.create_exit_handle();
        let session_monitor = session.create_session_monitor();

        current_thread.block_on(async move {
            let _ = tokio::join!(session.run(), async move {
                CommandInvokerTester::test_command_invoker(
                    test_case,
                    managed_client,
                    session_monitor,
                    mqtt_hub,
                )
                .await;
                exit_handle.try_exit().unwrap();
            });
        });
    }

    Ok(())
}

#[allow(clippy::unnecessary_wraps)]
#[allow(clippy::needless_pass_by_value)]
fn test_command_executor_session(_path: &Path, contents: String) -> datatest_stable::Result<()> {
    let wrapped_test_case: serde_yaml::Result<TestCase<ExecutorDefaults>> =
        serde_yaml::from_str(contents.as_str());

    assert!(wrapped_test_case.is_ok());
    let test_case = wrapped_test_case.unwrap();

    let test_case_index = TEST_CASE_INDEX.fetch_add(1, atomic::Ordering::Relaxed);

    if !PROBLEMATIC_TEST_CASES.contains(&test_case.test_name.as_str())
        && does_session_support(&test_case.requires)
    {
        let mqtt_client_id =
            get_client_id(&test_case, "SessionExecutorTestClient", test_case_index);
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id(mqtt_client_id)
            .hostname("localhost")
            .tcp_port(1883u16)
            .use_tls(false)
            .build()?;
        let incoming_packets_tx = IncomingPacketsTx::default();
        let outgoing_packets_rx = OutgoingPacketsRx::default();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .injected_packet_channels(Some(InjectedPacketChannels {
                incoming_packets_tx: incoming_packets_tx.clone(),
                outgoing_packets_rx: outgoing_packets_rx.clone(),
            }))
            .build()?;
        let session = Session::new(session_options).unwrap();
        let mqtt_hub = MqttHub::new(
            MqttEmulationLevel::Event,
            incoming_packets_tx,
            outgoing_packets_rx,
        );
        let managed_client = session.create_managed_client();

        let current_thread = Builder::new_current_thread().enable_all().build().unwrap();

        let exit_handle = session.create_exit_handle();
        let session_monitor = session.create_session_monitor();

        current_thread.block_on(async move {
            let _ = tokio::join!(session.run(), async move {
                CommandExecutorTester::test_command_executor(
                    test_case,
                    managed_client,
                    session_monitor,
                    mqtt_hub,
                )
                .await;
                exit_handle.try_exit().unwrap();
            });
        });
    }

    Ok(())
}

#[allow(clippy::unnecessary_wraps)]
#[allow(clippy::needless_pass_by_value)]
fn test_telemetry_receiver_session(_path: &Path, contents: String) -> datatest_stable::Result<()> {
    let wrapped_test_case: serde_yaml::Result<TestCase<ReceiverDefaults>> =
        serde_yaml::from_str(contents.as_str());

    assert!(wrapped_test_case.is_ok());
    let test_case = wrapped_test_case.unwrap();

    let test_case_index = TEST_CASE_INDEX.fetch_add(1, atomic::Ordering::Relaxed);

    if !PROBLEMATIC_TEST_CASES.contains(&test_case.test_name.as_str())
        && does_session_support(&test_case.requires)
    {
        let mqtt_client_id =
            get_client_id(&test_case, "SessionReceiverTestClient", test_case_index);
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id(mqtt_client_id)
            .hostname("localhost")
            .tcp_port(1883u16)
            .use_tls(false)
            .build()?;
        let incoming_packets_tx = IncomingPacketsTx::default();
        let outgoing_packets_rx = OutgoingPacketsRx::default();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .injected_packet_channels(Some(InjectedPacketChannels {
                incoming_packets_tx: incoming_packets_tx.clone(),
                outgoing_packets_rx: outgoing_packets_rx.clone(),
            }))
            .build()?;
        let session = Session::new(session_options).unwrap();
        let mqtt_hub = MqttHub::new(
            MqttEmulationLevel::Event,
            incoming_packets_tx,
            outgoing_packets_rx,
        );
        let managed_client = session.create_managed_client();

        let current_thread = Builder::new_current_thread().enable_all().build().unwrap();

        let exit_handle = session.create_exit_handle();
        let session_monitor = session.create_session_monitor();

        current_thread.block_on(async move {
            let _ = tokio::join!(session.run(), async move {
                TelemetryReceiverTester::test_telemetry_receiver(
                    test_case,
                    managed_client,
                    session_monitor,
                    mqtt_hub,
                )
                .await;
                exit_handle.try_exit().unwrap();
            });
        });
    }

    Ok(())
}

#[allow(clippy::unnecessary_wraps)]
#[allow(clippy::needless_pass_by_value)]
fn test_telemetry_sender_session(_path: &Path, contents: String) -> datatest_stable::Result<()> {
    let wrapped_test_case: serde_yaml::Result<TestCase<SenderDefaults>> =
        serde_yaml::from_str(contents.as_str());

    assert!(wrapped_test_case.is_ok());
    let test_case = wrapped_test_case.unwrap();

    let test_case_index = TEST_CASE_INDEX.fetch_add(1, atomic::Ordering::Relaxed);

    if !PROBLEMATIC_TEST_CASES.contains(&test_case.test_name.as_str())
        && does_session_support(&test_case.requires)
    {
        let mqtt_client_id = get_client_id(&test_case, "SessionSenderTestClient", test_case_index);
        let connection_settings = MqttConnectionSettingsBuilder::default()
            .client_id(mqtt_client_id)
            .hostname("localhost")
            .tcp_port(1883u16)
            .use_tls(false)
            .build()?;
        let incoming_packets_tx = IncomingPacketsTx::default();
        let outgoing_packets_rx = OutgoingPacketsRx::default();
        let session_options = SessionOptionsBuilder::default()
            .connection_settings(connection_settings)
            .injected_packet_channels(Some(InjectedPacketChannels {
                incoming_packets_tx: incoming_packets_tx.clone(),
                outgoing_packets_rx: outgoing_packets_rx.clone(),
            }))
            .build()?;
        let session = Session::new(session_options).unwrap();
        let mqtt_hub = MqttHub::new(
            MqttEmulationLevel::Event,
            incoming_packets_tx,
            outgoing_packets_rx,
        );
        let managed_client = session.create_managed_client();

        let current_thread = Builder::new_current_thread().enable_all().build().unwrap();

        let exit_handle = session.create_exit_handle();
        let session_monitor = session.create_session_monitor();

        current_thread.block_on(async move {
            let _ = tokio::join!(session.run(), async move {
                TelemetrySenderTester::test_telemetry_sender(
                    test_case,
                    managed_client,
                    session_monitor,
                    mqtt_hub,
                )
                .await;
                exit_handle.try_exit().unwrap();
            });
        });
    }

    Ok(())
}

/*
fn does_standalone_support(requirements: &[TestFeatureKind]) -> bool {
    !requirements.contains(&TestFeatureKind::Unobtanium)
        && !requirements.contains(&TestFeatureKind::AckOrdering)
        && !requirements.contains(&TestFeatureKind::Reconnection)
        && !requirements.contains(&TestFeatureKind::Caching)
        && !requirements.contains(&TestFeatureKind::Dispatch)
        && !requirements.contains(&TestFeatureKind::MultipleSerializers)
}
*/

fn does_session_support(requirements: &[TestFeatureKind]) -> bool {
    !requirements.contains(&TestFeatureKind::Unobtanium)
        && !requirements.contains(&TestFeatureKind::Dispatch)
        && !requirements.contains(&TestFeatureKind::MultipleSerializers)
}

fn get_client_id<T: DefaultsType + Default>(
    test_case: &TestCase<T>,
    client_id_base: &str,
    test_case_index: i32,
) -> String {
    if let Some(client_id) = test_case.prologue.mqtt_config.client_id.as_ref() {
        client_id.clone()
    } else {
        format!("{client_id_base}{test_case_index}")
    }
}

datatest_stable::harness!(
    test_command_invoker_session,
    "../../eng/test/test-cases/Protocol/CommandInvoker",
    r"^.*\.yaml",
    test_command_executor_session,
    "../../eng/test/test-cases/Protocol/CommandExecutor",
    r"^.*\.yaml",
    test_telemetry_receiver_session,
    "../../eng/test/test-cases/Protocol/TelemetryReceiver",
    r"^.*\.yaml",
    test_telemetry_sender_session,
    "../../eng/test/test-cases/Protocol/TelemetrySender",
    r"^.*\.yaml",
);
