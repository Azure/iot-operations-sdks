// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "leased_lock")]

use std::{env, sync::Arc, time::Duration};

use env_logger::Builder;

use tokio::{sync::Notify, time::sleep, time::timeout};

use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::leased_lock::{self};
use azure_iot_operations_services::state_store::{self};

// API:
// try_acquire
// acquire
// release
// observe/unobserve
// get_holder

// Test Scenarios:
// basic try acquire
// single holder acquires a lease
// two holders attempt to acquire a lease simultaneously, with release
// two holders attempt to acquire a lease, first renews lease
// second holder acquires non-released expired lease.
// second holder observes until lease released
// second holder observes until lease expires
// single holder attempts to release a lease twice
// attempt to observe lease that does not exist

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
    leased_lock::lease::Client<SessionManagedClient>,
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

    let lease_client = leased_lock::lease::Client::new(
        state_store_client.clone(),
        key_name.into(),
        client_id.into(),
    )
    .unwrap();

    (session, state_store_client, lease_client, exit_handle)
}

#[tokio::test]
async fn lease_basic_try_acquire_network_tests() {
    let test_id = "lease_basic_try_acquire_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let (session, state_store_client, lease_client, exit_handle) =
        initialize_client(test_id, &format!("{test_id}-lock"));

    let test_task = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(5);

            let _ = lease_client
                .try_acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            // Shutdown state store client and underlying resources
            assert!(state_store_client.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
async fn lease_single_holder_acquires_a_lock_network_tests() {
    let test_id = "lease_single_holder_acquires_a_lock_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let holder_name1 = format!("{test_id}1");
    let key_name1 = format!("{test_id}-lock");

    let (session, state_store_client, lease_client, exit_handle) =
        initialize_client(&holder_name1, &key_name1);

    let test_task = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(5);

            let _ = lease_client
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            let get_holder_response = lease_client.get_holder(request_timeout).await.unwrap();
            assert_eq!(
                get_holder_response.response.unwrap(),
                holder_name1.as_bytes().to_vec()
            );

            // Shutdown state store client and underlying resources
            assert!(state_store_client.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
async fn lease_two_holders_attempt_to_acquire_simultaneously_with_release_network_tests() {
    let test_id = "lease_two_holders_attempt_to_acquire_simultaneously_with_release_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let key_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (session1, state_store_client1, lease_client1, exit_handle1) =
        initialize_client(&holder_name1, &key_name1.clone());

    let (session2, state_store_client2, lease_client2, exit_handle2) =
        initialize_client(&holder_name2, &key_name1);

    let task1_notify = Arc::new(Notify::new());
    let task2_notify = task1_notify.clone();

    let test_task1_holder_name2 = holder_name2.clone();
    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(5);
            let request_timeout = Duration::from_secs(50);

            let _ = lease_client1
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            task1_notify.notify_one(); // Let task2 get holder name.
            task1_notify.notified().await; // Wait task2 get holder name.

            let release_result = lease_client1.release(request_timeout).await;
            assert!(release_result.is_ok());

            sleep(Duration::from_secs(1)).await; // Wait task2 acquire.

            let get_holder_response = lease_client1.get_holder(request_timeout).await.unwrap();
            assert_eq!(
                get_holder_response.response.unwrap(),
                test_task1_holder_name2.into_bytes()
            );

            // Shutdown state store client and underlying resources
            assert!(state_store_client1.shutdown().await.is_ok());

            exit_handle1.try_exit().await.unwrap();
        }
    });

    let test_task1_holder_name1 = holder_name1.clone();
    let test_task2 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(5);
            let request_timeout = Duration::from_secs(50);

            task2_notify.notified().await; // Wait task1 acquire.

            let get_holder_response = lease_client2.get_holder(request_timeout).await.unwrap();
            assert_eq!(
                get_holder_response.response.unwrap(),
                test_task1_holder_name1.into_bytes()
            );

            let _ = lease_client2
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            task2_notify.notify_one(); // Tell task1 we acquired, they can get holder name.

            // Shutdown state store client and underlying resources
            assert!(state_store_client2.shutdown().await.is_ok());

            exit_handle2.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task1.await.map_err(|e| { e.to_string() }) },
            async move { session1.run().await.map_err(|e| { e.to_string() }) },
            async move { test_task2.await.map_err(|e| { e.to_string() }) },
            async move { session2.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
async fn lease_two_holders_attempt_to_acquire_first_renews_network_tests() {
    let test_id = "lease_two_holders_attempt_to_acquire_first_renews_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let key_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (session1, state_store_client1, lease_client1, exit_handle1) =
        initialize_client(&holder_name1, &key_name1.clone());

    let (session2, state_store_client2, lease_client2, exit_handle2) =
        initialize_client(&holder_name2, &key_name1);

    let task1_notify = Arc::new(Notify::new());
    let task2_notify = task1_notify.clone();

    let test_task1_holder_name2 = holder_name2.clone();
    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(5);
            let request_timeout = Duration::from_secs(50);

            let _ = lease_client1
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            sleep(Duration::from_secs(2)).await;
            task1_notify.notify_one(); // [A] Tell task2 lock was acquired by task1.

            let _ = lease_client1
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            task1_notify.notified().await; // [B] Wait task2 get holder name.

            let release_result = lease_client1.release(request_timeout).await;
            assert!(release_result.is_ok());

            task1_notify.notified().await; // [C] Wait task2 acquire.

            let get_holder_response = lease_client1.get_holder(request_timeout).await.unwrap();
            assert_eq!(
                get_holder_response.response.unwrap(),
                test_task1_holder_name2.into_bytes()
            );

            // Shutdown state store client and underlying resources
            assert!(state_store_client1.shutdown().await.is_ok());

            exit_handle1.try_exit().await.unwrap();
        }
    });

    let test_task1_holder_name1 = holder_name1.clone();
    let test_task2 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(4);
            let request_timeout = Duration::from_secs(50);

            task2_notify.notified().await; // [A] Wait task1 acquire.

            let get_holder_response = lease_client2.get_holder(request_timeout).await.unwrap();
            assert_eq!(
                get_holder_response.response.unwrap(),
                test_task1_holder_name1.clone().into_bytes()
            );

            sleep(Duration::from_secs(2)).await;

            let get_holder_response2 = lease_client2.get_holder(request_timeout).await.unwrap();
            assert_eq!(
                get_holder_response2.response.unwrap(),
                test_task1_holder_name1.into_bytes()
            );

            task2_notify.notify_one(); // [B] Tell task1 to releasee lock.

            let _ = lease_client2
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            task2_notify.notify_one(); // [C] Tell task1 lock is acquired.

            // Shutdown state store client and underlying resources
            assert!(state_store_client2.shutdown().await.is_ok());

            exit_handle2.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task1.await.map_err(|e| { e.to_string() }) },
            async move { session1.run().await.map_err(|e| { e.to_string() }) },
            async move { test_task2.await.map_err(|e| { e.to_string() }) },
            async move { session2.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
async fn lease_second_holder_acquires_non_released_expired_lock_network_tests() {
    let test_id = "lease_second_holder_acquires_non_released_expired_lock_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let key_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (session1, state_store_client1, lease_client1, exit_handle1) =
        initialize_client(&holder_name1, &key_name1.clone());

    let (session2, state_store_client2, lease_client2, exit_handle2) =
        initialize_client(&holder_name2, &key_name1);

    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(50);

            let _ = lease_client1
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            sleep(Duration::from_secs(4)).await; // This will allow the lock to expire.

            // Shutdown state store client and underlying resources
            assert!(state_store_client1.shutdown().await.is_ok());

            exit_handle1.try_exit().await.unwrap();
        }
    });

    let test_task2 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(30);
            let request_timeout = Duration::from_secs(50);

            sleep(Duration::from_secs(5)).await;

            let _ = lease_client2
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            // Shutdown state store client and underlying resources
            assert!(state_store_client2.shutdown().await.is_ok());

            exit_handle2.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task1.await.map_err(|e| { e.to_string() }) },
            async move { session1.run().await.map_err(|e| { e.to_string() }) },
            async move { test_task2.await.map_err(|e| { e.to_string() }) },
            async move { session2.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
async fn lease_second_holder_observes_until_lock_is_released_network_tests() {
    let test_id = "lease_second_holder_observes_until_lock_is_released_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let key_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (session1, state_store_client1, lease_client1, exit_handle1) =
        initialize_client(&holder_name1, &key_name1.clone());

    let (session2, state_store_client2, lease_client2, exit_handle2) =
        initialize_client(&holder_name2, &key_name1);

    let task1_notify = Arc::new(Notify::new());
    let task2_notify = task1_notify.clone();

    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(4);
            let request_timeout = Duration::from_secs(50);

            let _ = lease_client1
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            task1_notify.notify_one(); // [A] Tell task2 lock was acquired.

            sleep(Duration::from_secs(2)).await; // [B] Wait task2 observe lock.

            let release_result = lease_client1.release(request_timeout).await;
            assert!(release_result.is_ok());

            // Shutdown state store client and underlying resources
            assert!(state_store_client1.shutdown().await.is_ok());

            exit_handle1.try_exit().await.unwrap();
        }
    });

    let test_task2_key_name1 = key_name1.clone();
    let test_task2 = tokio::task::spawn({
        async move {
            let request_timeout = Duration::from_secs(50);

            task2_notify.notified().await; // [A] Wait task1 acquire.

            let mut observe_response = lease_client2.observe(request_timeout).await.unwrap();

            let receive_notifications_task = tokio::task::spawn({
                async move {
                    let Some((notification, _)) =
                        observe_response.response.recv_notification().await
                    else {
                        panic!("Received unexpected None for notification");
                    };

                    assert_eq!(notification.key, test_task2_key_name1.clone().into_bytes());
                    assert_eq!(notification.operation, state_store::Operation::Del);
                }
            });

            // [B] Wait task1 delay elapse...
            assert!(
                timeout(Duration::from_secs(5), receive_notifications_task)
                    .await
                    .is_ok()
            );

            // Shutdown state store client and underlying resources
            assert!(state_store_client2.shutdown().await.is_ok());

            exit_handle2.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task1.await.map_err(|e| { e.to_string() }) },
            async move { session1.run().await.map_err(|e| { e.to_string() }) },
            async move { test_task2.await.map_err(|e| { e.to_string() }) },
            async move { session2.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
async fn lease_shutdown_state_store_while_observing_lock_network_tests() {
    let test_id = "lease_shutdown_state_store_while_observing_lock_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let key_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");

    let (session1, state_store_client1, lease_client1, exit_handle1) =
        initialize_client(&holder_name1, &key_name1.clone());

    let test_task1 = tokio::task::spawn({
        async move {
            let request_timeout = Duration::from_secs(50);

            let mut observe_response = lease_client1.observe(request_timeout).await.unwrap();

            let receive_notifications_task = tokio::task::spawn({
                async move { observe_response.response.recv_notification().await }
            });

            assert!(state_store_client1.shutdown().await.is_ok());

            let receive_notifications_result = receive_notifications_task.await;
            assert!(receive_notifications_result.is_ok());
            assert!(receive_notifications_result.unwrap().is_none());

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
async fn lease_second_holder_observes_until_lock_expires_network_tests() {
    let test_id = "lease_second_holder_observes_until_lock_expires_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let key_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (session1, state_store_client1, lease_client1, exit_handle1) =
        initialize_client(&holder_name1, &key_name1.clone());

    let (session2, state_store_client2, lease_client2, exit_handle2) =
        initialize_client(&holder_name2, &key_name1);

    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(4);
            let request_timeout = Duration::from_secs(50);

            let _ = lease_client1
                .acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            // Shutdown state store client and underlying resources
            assert!(state_store_client1.shutdown().await.is_ok());

            exit_handle1.try_exit().await.unwrap();
        }
    });

    let test_task2_key_name1 = key_name1.clone();
    let test_task2 = tokio::task::spawn({
        async move {
            let request_timeout = Duration::from_secs(50);

            sleep(Duration::from_secs(1)).await;

            let mut observe_response = lease_client2.observe(request_timeout).await.unwrap();

            let receive_notifications_task = tokio::task::spawn({
                async move {
                    let Some((notification, _)) =
                        observe_response.response.recv_notification().await
                    else {
                        panic!("Received unexpected None for notification.");
                    };

                    assert_eq!(notification.key, test_task2_key_name1.clone().into_bytes());
                    assert_eq!(notification.operation, state_store::Operation::Del);
                }
            });

            assert!(
                timeout(Duration::from_secs(5), receive_notifications_task)
                    .await
                    .is_ok()
            );

            // Shutdown state store client and underlying resources
            assert!(state_store_client2.shutdown().await.is_ok());

            exit_handle2.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task1.await.map_err(|e| { e.to_string() }) },
            async move { session1.run().await.map_err(|e| { e.to_string() }) },
            async move { test_task2.await.map_err(|e| { e.to_string() }) },
            async move { session2.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
async fn lease_attempt_to_release_twice_network_tests() {
    let test_id = "lease_attempt_to_release_twice_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let (session, state_store_client, lease_client, exit_handle) =
        initialize_client(&format!("{test_id}1"), &format!("{test_id}-lock"));

    let test_task = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(5);

            let _ = lease_client
                .try_acquire(lock_expiry, request_timeout)
                .await
                .unwrap();

            let release_result = lease_client.release(request_timeout).await;
            assert!(release_result.is_ok());

            let release_result2 = lease_client.release(request_timeout).await;
            assert!(release_result2.is_ok());

            // Shutdown state store client and underlying resources
            assert!(state_store_client.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}

#[tokio::test]
async fn lease_attempt_to_observe_that_does_not_exist_network_tests() {
    let test_id = "lease_attempt_to_observe_that_does_not_exist_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let (session, state_store_client, lease_client, exit_handle) =
        initialize_client(&format!("{test_id}1"), &format!("{test_id}-lock"));

    let test_task = tokio::task::spawn({
        async move {
            let request_timeout = Duration::from_secs(5);

            let _observe_response = lease_client.observe(request_timeout).await.unwrap();
            // Looks like this never fails. That is expected:
            // vaavva: "Since a key being deleted doesn't end your observation,
            // it makes sense that if you observe a key that doesn't exist,
            // you might expect it to exist in the future and want notifications"

            // Shutdown state store client and underlying resources
            assert!(state_store_client.shutdown().await.is_ok());

            exit_handle.try_exit().await.unwrap();
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(
        tokio::try_join!(
            async move { test_task.await.map_err(|e| { e.to_string() }) },
            async move { session.run().await.map_err(|e| { e.to_string() }) }
        )
        .is_ok()
    );
}
