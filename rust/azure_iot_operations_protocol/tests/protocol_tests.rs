// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

mod metl;

use std::path::Path;
use std::sync::atomic;

use azure_iot_operations_mqtt::session::{
    managed_client::SessionManagedClient, reconnect_policy::ExponentialBackoffWithJitter,
    session::Session,
};
use tokio::runtime::Builder;

use metl::command_executor_tester::CommandExecutorTester;
use metl::command_invoker_tester::CommandInvokerTester;
use metl::defaults::{
    DefaultsType, ExecutorDefaults, InvokerDefaults, ReceiverDefaults, SenderDefaults,
};
use metl::mqtt_driver::MqttDriver;
use metl::mqtt_emulation_level::MqttEmulationLevel;
use metl::mqtt_hub::MqttHub;
use metl::telemetry_receiver_tester::TelemetryReceiverTester;
use metl::telemetry_sender_tester::TelemetrySenderTester;
use metl::test_case::TestCase;
use metl::test_feature_kind::TestFeatureKind;

static TEST_CASE_INDEX: atomic::AtomicI32 = atomic::AtomicI32::new(0);

const PROBLEMATIC_TEST_CASES: &[&str] = &[
    "CommandExecutorRequestsCompleteOutOfOrder_RequestAckedInOrder",
    "CommandExecutorRequestExpiresWhileDisconnected_RequestNotAcknowledged",
    "CommandExecutorResponsePubAckDroppedByDisconnection_ReconnectAndSuccess",
    "CommandExecutorUserCodeRaisesContentError_RespondsError",
    "CommandExecutorUserCodeRaisesContentErrorWithDetails_RespondsError",
    "CommandExecutorRequest_TimeoutPropagated",
    "CommandInvokerInvalidResponseTopicPrefix_ThrowsException",
    "CommandInvokerInvalidResponseTopicSuffix_ThrowsException",
    "CommandInvokerPubAckDroppedByDisconnection_ReconnectAndSuccess",
    "CommandInvokerWithCustomResponseTopic_Success",
    "CommandInvokerWithSubMillisecTimeout_ThrowsException",
    "CommandInvokerWithZeroTimeout_ThrowsException",
    "TelemetrySenderPubAckDroppedByDisconnection_ReconnectAndSuccess",
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
                test_case_index,
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
                test_case_index,
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
        let mut mqtt_hub = MqttHub::new(mqtt_client_id.clone(), MqttEmulationLevel::Event);
        let mut session = Session::new_from_injection(
            mqtt_hub.get_driver(),
            mqtt_hub.get_looper(),
            Box::new(ExponentialBackoffWithJitter::default()),
            mqtt_client_id,
            None,
        );
        let managed_client = session.create_managed_client();

        let current_thread = Builder::new_current_thread().enable_all().build().unwrap();

        let exit_handle = session.create_exit_handle();

        current_thread.block_on(async move {
            let _ = tokio::join!(session.run(), async move {
                CommandInvokerTester::<SessionManagedClient<MqttDriver>>::test_command_invoker(
                    test_case,
                    test_case_index,
                    managed_client,
                    mqtt_hub,
                )
                .await;
                exit_handle.exit_force().await;
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
        let mut mqtt_hub = MqttHub::new(mqtt_client_id.clone(), MqttEmulationLevel::Event);
        let mut session = Session::new_from_injection(
            mqtt_hub.get_driver(),
            mqtt_hub.get_looper(),
            Box::new(ExponentialBackoffWithJitter::default()),
            mqtt_client_id,
            None,
        );
        let managed_client = session.create_managed_client();

        let current_thread = Builder::new_current_thread().enable_all().build().unwrap();

        let exit_handle = session.create_exit_handle();

        current_thread.block_on(async move {
            let _ = tokio::join!(session.run(), async move {
                CommandExecutorTester::<SessionManagedClient<MqttDriver>>::test_command_executor(
                    test_case,
                    test_case_index,
                    managed_client,
                    mqtt_hub,
                )
                .await;
                exit_handle.exit_force().await;
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
        let mut mqtt_hub = MqttHub::new(mqtt_client_id.clone(), MqttEmulationLevel::Event);
        let mut session = Session::new_from_injection(
            mqtt_hub.get_driver(),
            mqtt_hub.get_looper(),
            Box::new(ExponentialBackoffWithJitter::default()),
            mqtt_client_id,
            None,
        );
        let managed_client = session.create_managed_client();

        let current_thread = Builder::new_current_thread().enable_all().build().unwrap();

        let exit_handle = session.create_exit_handle();

        current_thread.block_on(async move {
            let _ = tokio::join!(session.run(), async move {
                TelemetryReceiverTester::<SessionManagedClient<MqttDriver>>::test_telemetry_receiver(
                    test_case,
                    test_case_index,
                    managed_client,
                    mqtt_hub,
                )
                .await;
                exit_handle.exit_force().await;
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
        let mut mqtt_hub = MqttHub::new(mqtt_client_id.clone(), MqttEmulationLevel::Event);
        let mut session = Session::new_from_injection(
            mqtt_hub.get_driver(),
            mqtt_hub.get_looper(),
            Box::new(ExponentialBackoffWithJitter::default()),
            mqtt_client_id,
            None,
        );
        let managed_client = session.create_managed_client();

        let current_thread = Builder::new_current_thread().enable_all().build().unwrap();

        let exit_handle = session.create_exit_handle();

        current_thread.block_on(async move {
            let _ = tokio::join!(session.run(), async move {
                TelemetrySenderTester::<SessionManagedClient<MqttDriver>>::test_telemetry_sender(
                    test_case,
                    test_case_index,
                    managed_client,
                    mqtt_hub,
                )
                .await;
                exit_handle.exit_force().await;
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
}
*/

fn does_session_support(requirements: &[TestFeatureKind]) -> bool {
    !requirements.contains(&TestFeatureKind::Unobtanium)
        && !requirements.contains(&TestFeatureKind::TopicFiltering)
        && !requirements.contains(&TestFeatureKind::Caching)
        && !requirements.contains(&TestFeatureKind::Dispatch)
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
