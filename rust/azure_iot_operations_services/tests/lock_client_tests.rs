// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "leased_lock")]

use std::assert_eq;
use std::{env, sync::Arc, time::Duration};

use env_logger::Builder;

use tokio::time::sleep;

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::leased_lock::{lease, lock};
use azure_iot_operations_services::state_store::{self};

// API:
// lock
// unlock

// Test Scenarios:
// single holder do lock and release
// single holder do lock and release with auto-renewal

fn setup_test(test_name: &str) -> bool {
    let _ = Builder::new()
        .filter_level(log::LevelFilter::Info)
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations", log::LevelFilter::Warn)
        .try_init();

    if env::var("ENABLE_NETWORK_TESTS").is_err() {
        log::warn!("Test {test_name} is skipped. Set ENABLE_NETWORK_TESTS to run.");
        return false;
    }

    true
}

fn initialize_client(
    client_id: &str,
    key_name: &str,
) -> (
    Session,
    Arc<state_store::Client<SessionManagedClient>>,
    lease::Client<SessionManagedClient>,
    lock::Client<SessionManagedClient>,
    SessionExitHandle,
) {
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
    let application_context = ApplicationContextBuilder::default().build().unwrap();

    let state_store_client = state_store::Client::new(
        application_context,
        session.create_managed_client(),
        session.create_connection_monitor(),
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    let state_store_client = Arc::new(state_store_client);

    let exit_handle: SessionExitHandle = session.create_exit_handle();

    let lease_client = lease::Client::new(
        state_store_client.clone(),
        key_name.into(),
        client_id.into(),
    )
    .unwrap();

    let leased_lock_client = lock::Client::new(
        state_store_client.clone(),
        key_name.into(),
        client_id.into(),
    )
    .unwrap();

    (
        session,
        state_store_client,
        lease_client,
        leased_lock_client,
        exit_handle,
    )
}

#[tokio::test]
async fn leased_lock_single_holder_do_lock_and_unlock_network_tests() {
    let test_id = "leased_lock_single_holder_do_lock_and_unlock_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let key_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let shared_resource_key_name = format!("{test_id}-key");

    let (session1, state_store_client1, lease_client1, mut leased_lock_client1, exit_handle1) =
        initialize_client(&holder_name1, &key_name1.clone());

    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(5);
            let request_timeout = Duration::from_secs(30);

            let token = leased_lock_client1
                .lock(lock_expiry, request_timeout, None)
                .await
                .expect("Expected a fencing token");

            // Let's verify if the fencing token was stored internally.
            let saved_fencing_token = leased_lock_client1.get_current_lock_fencing_token();

            assert!(saved_fencing_token.is_some());
            assert_eq!(token, saved_fencing_token.unwrap());

            // Validate holder.
            assert_eq!(
                lease_client1
                    .get_holder(request_timeout)
                    .await
                    .unwrap()
                    .response
                    .unwrap(),
                holder_name1.into_bytes()
            );

            assert!(leased_lock_client1.unlock(request_timeout).await.is_ok());

            assert!(
                state_store_client1
                    .get(shared_resource_key_name.into_bytes(), request_timeout)
                    .await
                    .unwrap()
                    .response
                    .is_none()
            );

            // Let's verify if the fencing token was cleared internally.
            assert!(
                leased_lock_client1
                    .get_current_lock_fencing_token()
                    .is_none()
            );

            // Shutdown state store client and underlying resources
            assert!(state_store_client1.shutdown().await.is_ok());

            exit_handle1.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task1.await.map_err(|e| { e.to_string() }) },
            async move { session1.run().await.map_err(|e| { e.to_string() }) },
        )
        .is_ok()
    );
}

#[tokio::test]
async fn leased_lock_single_holder_do_lock_with_auto_renewal_network_tests() {
    let test_id = "leased_lock_single_holder_do_lock_with_auto_renewal_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let key_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");

    let (session1, state_store_client1, _lease_client1, mut leased_lock_client1, exit_handle1) =
        initialize_client(&holder_name1, &key_name1.clone());

    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(10);
            let renewal_period = Duration::from_secs(2);

            let fencing_token1 = leased_lock_client1
                .lock(lock_expiry, request_timeout, Some(renewal_period))
                .await
                .expect("Expected a fencing token");

            // Wait for renewal at 2 seconds even if expiry time has passed.
            sleep(Duration::from_secs(3)).await;

            // Expect to have a new token now (updated timestamp, but same counter and node id).
            let fencing_token2_option = leased_lock_client1.get_current_lock_fencing_token();

            assert!(fencing_token2_option.is_some());
            let fencing_token2 = fencing_token2_option.unwrap();
            assert!(fencing_token1.timestamp < fencing_token2.timestamp);
            assert_eq!(fencing_token1.node_id, fencing_token2.node_id);

            // Shutdown state store client and underlying resources
            assert!(state_store_client1.shutdown().await.is_ok());

            exit_handle1.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task1.await.map_err(|e| { e.to_string() }) },
            async move { session1.run().await.map_err(|e| { e.to_string() }) },
        )
        .is_ok()
    );
}
