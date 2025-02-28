// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "leased_lock")]

use std::{env, sync::Arc, time::Duration};

use env_logger::Builder;

use tokio::{sync::Mutex, time::sleep, time::timeout};

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::ApplicationContextBuilder;
use azure_iot_operations_services::leased_lock::{self};
use azure_iot_operations_services::state_store::{self};

// API:
// try_acquire_lock
// acquire_lock
// release_lock
// observe_lock/unobserve_lock
// acquire_lock_and_update_value
// acquire_lock_and_delete_value
// get_lock_holder

// Test Scenarios:
// basic try lock
// single holder acquires a lock
// two holders attempt to acquire a lock simultaneously, with release
// two holders attempt to acquire a lock, first renews lease
// second holder acquires non-released expired lock.
// second holder observes until lock released
// second holder observes until lock expires
// single holder do acquire_lock_and_update_value
// two holders do acquire_lock_and_update_value
// single holder do acquire_lock_and_delete_value
// single holder attempts to release a lock twice
// attempt to observe lock that does not exist

fn setup_test(test_name: &str) -> bool {
    let _ = Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations", log::LevelFilter::Warn)
        .try_init();

    if env::var("ENABLE_NETWORK_TESTS").is_err() {
        log::warn!("Test {test_name} is skipped. Set ENABLE_NETWORK_TESTS to run.");
        return false;
    } else {
        return true;
    }
}

fn initialize_client(
    client_id: &str,
    lock_name: &str,
) -> Result<
    (
        Session,
        Arc<Mutex<state_store::Client<SessionManagedClient>>>,
        leased_lock::Client<SessionManagedClient>,
        SessionExitHandle,
    ),
    (),
> {
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
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();

    let state_store_client_arc_mutex = Arc::new(Mutex::new(state_store_client));

    let exit_handle: SessionExitHandle = session.create_exit_handle();

    let leased_lock_client = leased_lock::Client::new(
        state_store_client_arc_mutex.clone(),
        lock_name.into(),
        client_id.into(),
    )
    .unwrap();

    Ok((
        session,
        state_store_client_arc_mutex,
        leased_lock_client,
        exit_handle,
    ))
}

#[tokio::test]
async fn leased_lock_basic_try_acquire_network_tests() {
    let test_id = "leased_lock_basic_try_acquire_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let (mut session, state_store_client_arc_mutex, leased_lock_client, exit_handle) =
        initialize_client(&format!("{test_id}"), &format!("{test_id}-lock")).unwrap();

    let test_task = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(5);

            let try_acquire_response = leased_lock_client
                .try_acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(try_acquire_response.response);

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task.await.map_err(|e| { e.to_string() }) },
        async move { session.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[tokio::test]
async fn leased_lock_single_holder_acquires_a_lock_network_tests() {
    let test_id = "leased_lock_single_holder_acquires_a_lock_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let holder_name1 = format!("{test_id}1");
    let lock_name1 = format!("{test_id}-lock");

    let (mut session, state_store_client_arc_mutex, leased_lock_client, exit_handle) =
        match initialize_client(&holder_name1, &lock_name1) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(5);

            let acquire_response = leased_lock_client
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            let get_lock_holder_response = leased_lock_client
                .get_lock_holder(lock_name1.as_bytes().to_vec(), request_timeout)
                .await
                .unwrap();
            assert_eq!(
                get_lock_holder_response.response.unwrap(),
                holder_name1.as_bytes().to_vec()
            );

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task.await.map_err(|e| { e.to_string() }) },
        async move { session.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[tokio::test]
async fn leased_lock_two_holders_attempt_to_acquire_lock_simultaneously_with_release_network_tests()
{
    let test_id =
        "leased_lock_two_holders_attempt_to_acquire_lock_simultaneously_with_release_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let lock_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (mut session1, state_store_client_arc_mutex1, leased_lock_client1, exit_handle1) =
        match initialize_client(&holder_name1, &lock_name1.clone()) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let (mut session2, state_store_client_arc_mutex2, leased_lock_client2, exit_handle2) =
        match initialize_client(&holder_name2, &lock_name1) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task1_lock_name1 = lock_name1.clone();
    let test_task1_holder_name2 = holder_name2.clone();
    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(10);
            let request_timeout = Duration::from_secs(50);

            let acquire_response = leased_lock_client1
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            sleep(Duration::from_secs(3)).await;

            let release_response = leased_lock_client1
                .release_lock(request_timeout)
                .await
                .unwrap();
            assert_eq!(release_response.response, 1);

            sleep(Duration::from_secs(2)).await;

            let get_lock_holder_response = leased_lock_client1
                .get_lock_holder(test_task1_lock_name1.clone().into_bytes(), request_timeout)
                .await
                .unwrap();
            assert_eq!(
                get_lock_holder_response.response.unwrap(),
                test_task1_holder_name2.into_bytes()
            );

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex1.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle1.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    let test_task2_lock_name1 = lock_name1.clone();
    let test_task1_holder_name1 = holder_name1.clone();
    let test_task2 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(30);
            let request_timeout = Duration::from_secs(50);

            sleep(Duration::from_secs(1)).await;

            let get_lock_holder_response = leased_lock_client2
                .get_lock_holder(test_task2_lock_name1.clone().into_bytes(), request_timeout)
                .await
                .unwrap();
            assert_eq!(
                get_lock_holder_response.response.unwrap(),
                test_task1_holder_name1.into_bytes()
            );

            let acquire_response = leased_lock_client2
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex2.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle2.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task1.await.map_err(|e| { e.to_string() }) },
        async move { session1.run().await.map_err(|e| { e.to_string() }) },
        async move { test_task2.await.map_err(|e| { e.to_string() }) },
        async move { session2.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[tokio::test]
async fn leased_lock_two_holders_attempt_to_acquire_lock_first_renews_network_tests() {
    let test_id = "leased_lock_two_holders_attempt_to_acquire_lock_first_renews_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let lock_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (mut session1, state_store_client_arc_mutex1, leased_lock_client1, exit_handle1) =
        match initialize_client(&holder_name1, &lock_name1.clone()) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let (mut session2, state_store_client_arc_mutex2, leased_lock_client2, exit_handle2) =
        match initialize_client(&holder_name2, &lock_name1) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task1_lock_name1 = lock_name1.clone();
    let test_task1_holder_name2 = holder_name2.clone();
    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(5);
            let request_timeout = Duration::from_secs(50);

            let acquire_response = leased_lock_client1
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            sleep(Duration::from_secs(2)).await;

            let acquire_response2 = leased_lock_client1
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response2.response);

            sleep(Duration::from_secs(2)).await;

            let release_response = leased_lock_client1
                .release_lock(request_timeout)
                .await
                .unwrap();
            assert_eq!(release_response.response, 1);

            sleep(Duration::from_secs(2)).await;

            let get_lock_holder_response = leased_lock_client1
                .get_lock_holder(test_task1_lock_name1.clone().into_bytes(), request_timeout)
                .await
                .unwrap();
            assert_eq!(
                get_lock_holder_response.response.unwrap(),
                test_task1_holder_name2.into_bytes()
            );

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex1.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle1.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    let test_task2_lock_name1 = lock_name1.clone();
    let test_task1_holder_name1 = holder_name1.clone();
    let test_task2 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(30);
            let request_timeout = Duration::from_secs(50);

            sleep(Duration::from_secs(1)).await;

            let get_lock_holder_response = leased_lock_client2
                .get_lock_holder(test_task2_lock_name1.clone().into_bytes(), request_timeout)
                .await
                .unwrap();
            assert_eq!(
                get_lock_holder_response.response.unwrap(),
                test_task1_holder_name1.clone().into_bytes()
            );

            sleep(Duration::from_secs(2)).await;

            let get_lock_holder_response2 = leased_lock_client2
                .get_lock_holder(test_task2_lock_name1.clone().into_bytes(), request_timeout)
                .await
                .unwrap();
            assert_eq!(
                get_lock_holder_response2.response.unwrap(),
                test_task1_holder_name1.into_bytes()
            );

            let acquire_response = leased_lock_client2
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex2.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle2.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task1.await.map_err(|e| { e.to_string() }) },
        async move { session1.run().await.map_err(|e| { e.to_string() }) },
        async move { test_task2.await.map_err(|e| { e.to_string() }) },
        async move { session2.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[tokio::test]
async fn leased_lock_second_holder_acquires_non_released_expired_lock_network_tests() {
    let test_id = "leased_lock_second_holder_acquires_non_released_expired_lock_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let lock_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (mut session1, state_store_client_arc_mutex1, leased_lock_client1, exit_handle1) =
        match initialize_client(&holder_name1, &lock_name1.clone()) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let (mut session2, state_store_client_arc_mutex2, leased_lock_client2, exit_handle2) =
        match initialize_client(&holder_name2, &lock_name1) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(50);

            let acquire_response = leased_lock_client1
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            sleep(Duration::from_secs(4)).await; // This will allow the lock to expire.

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex1.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle1.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    let test_task2 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(30);
            let request_timeout = Duration::from_secs(50);

            sleep(Duration::from_secs(5)).await;

            let acquire_response = leased_lock_client2
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex2.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle2.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task1.await.map_err(|e| { e.to_string() }) },
        async move { session1.run().await.map_err(|e| { e.to_string() }) },
        async move { test_task2.await.map_err(|e| { e.to_string() }) },
        async move { session2.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[tokio::test]
async fn leased_lock_second_holder_observes_until_lock_is_released_network_tests() {
    let test_id = "leased_lock_second_holder_observes_until_lock_is_released_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let lock_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (mut session1, state_store_client_arc_mutex1, leased_lock_client1, exit_handle1) =
        match initialize_client(&holder_name1, &lock_name1.clone()) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let (mut session2, state_store_client_arc_mutex2, leased_lock_client2, exit_handle2) =
        match initialize_client(&holder_name2, &lock_name1) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(120);
            let request_timeout = Duration::from_secs(50);

            let acquire_response = leased_lock_client1
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            sleep(Duration::from_secs(4)).await;

            let release_response = leased_lock_client1
                .release_lock(request_timeout)
                .await
                .unwrap();
            assert_eq!(release_response.response, 1);

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex1.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle1.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    let test_task2_lock_name1 = lock_name1.clone();
    let test_task2 = tokio::task::spawn({
        async move {
            let request_timeout = Duration::from_secs(50);

            sleep(Duration::from_secs(1)).await;

            let mut observe_response = leased_lock_client2
                .observe_lock(request_timeout)
                .await
                .unwrap();

            let receive_notifications_task = tokio::task::spawn({
                async move {
                    while let Some((notification, _)) =
                        observe_response.response.recv_notification().await
                    {
                        assert_eq!(notification.key, test_task2_lock_name1.clone().into_bytes());
                        assert_eq!(notification.operation, state_store::Operation::Del);
                        break;
                    }
                }
            });

            assert!(timeout(Duration::from_secs(10), receive_notifications_task)
                .await
                .is_ok());

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex2.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle2.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task1.await.map_err(|e| { e.to_string() }) },
        async move { session1.run().await.map_err(|e| { e.to_string() }) },
        async move { test_task2.await.map_err(|e| { e.to_string() }) },
        async move { session2.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[tokio::test]
async fn leased_lock_second_holder_observes_until_lock_expires_network_tests() {
    let test_id = "leased_lock_second_holder_observes_until_lock_expires_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let lock_name1 = format!("{test_id}-lock");
    let holder_name1 = format!("{test_id}1");
    let holder_name2 = format!("{test_id}2");

    let (mut session1, state_store_client_arc_mutex1, leased_lock_client1, exit_handle1) =
        match initialize_client(&holder_name1, &lock_name1.clone()) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let (mut session2, state_store_client_arc_mutex2, leased_lock_client2, exit_handle2) =
        match initialize_client(&holder_name2, &lock_name1) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task1 = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(4);
            let request_timeout = Duration::from_secs(50);

            let acquire_response = leased_lock_client1
                .acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(acquire_response.response);

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex1.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle1.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    let test_task2_lock_name1 = lock_name1.clone();
    let test_task2 = tokio::task::spawn({
        async move {
            let request_timeout = Duration::from_secs(50);

            sleep(Duration::from_secs(1)).await;

            let mut observe_response = leased_lock_client2
                .observe_lock(request_timeout)
                .await
                .unwrap();

            let receive_notifications_task = tokio::task::spawn({
                async move {
                    while let Some((notification, _)) =
                        observe_response.response.recv_notification().await
                    {
                        assert_eq!(notification.key, test_task2_lock_name1.clone().into_bytes());
                        assert_eq!(notification.operation, state_store::Operation::Del);
                        break;
                    }
                }
            });

            assert!(timeout(Duration::from_secs(5), receive_notifications_task)
                .await
                .is_ok());

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex2.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle2.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task1.await.map_err(|e| { e.to_string() }) },
        async move { session1.run().await.map_err(|e| { e.to_string() }) },
        async move { test_task2.await.map_err(|e| { e.to_string() }) },
        async move { session2.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

// single holder do acquire_lock_and_update_value
// two holders do acquire_lock_and_update_value
// single holder do acquire_lock_and_delete_value

#[tokio::test]
async fn leased_lock_attempt_to_release_lock_twice_network_tests() {
    let test_id = "leased_lock_attempt_to_release_lock_twice_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let (mut session, state_store_client_arc_mutex, leased_lock_client, exit_handle) =
        match initialize_client(&format!("{test_id}1"), &format!("{test_id}-lock")) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task = tokio::task::spawn({
        async move {
            let lock_expiry = Duration::from_secs(3);
            let request_timeout = Duration::from_secs(5);

            let try_acquire_response = leased_lock_client
                .try_acquire_lock(lock_expiry, request_timeout)
                .await
                .unwrap();
            assert!(try_acquire_response.response);

            let release_response = leased_lock_client
                .release_lock(request_timeout)
                .await
                .unwrap();
            assert_eq!(release_response.response, 1);

            let release_response2 = leased_lock_client
                .release_lock(request_timeout)
                .await
                .unwrap();
            assert_eq!(release_response2.response, 0);

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task.await.map_err(|e| { e.to_string() }) },
        async move { session.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[tokio::test]
async fn leased_lock_attempt_to_observe_lock_that_does_not_exist_network_tests() {
    let test_id = "leased_lock_attempt_to_observe_lock_that_does_not_exist_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let (mut session, state_store_client_arc_mutex, leased_lock_client, exit_handle) =
        match initialize_client(&format!("{test_id}1"), &format!("{test_id}-lock")) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task = tokio::task::spawn({
        async move {
            let request_timeout = Duration::from_secs(5);

            let _observe_response = leased_lock_client
                .observe_lock(request_timeout)
                .await
                .unwrap();
            // Looks like this never fails. That is expected:
            // vaava: "Since a key being deleted doesn't end your observation,
            // it makes sense that if you observe a key that doesn't exist,
            // you might expect it to exist in the future and want notifications"

            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task.await.map_err(|e| { e.to_string() }) },
        async move { session.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}

#[ignore]
#[tokio::test]
async fn leased_lock_shutdown_right_away_network_tests() {
    let test_id = "leased_lock_shutdown_right_away_network_tests";
    if !setup_test(test_id) {
        return;
    }

    let (mut session, state_store_client_arc_mutex, _leased_lock_client, exit_handle) =
        match initialize_client(&format!("{test_id}1"), &format!("{test_id}-lock")) {
            Ok((a, b, c, d)) => (a, b, c, d),
            Err(error) => panic!("{:?}", error),
        };

    let test_task = tokio::task::spawn({
        async move {
            // Shutdown state store client and underlying resources
            let state_store_client = state_store_client_arc_mutex.lock().await;
            assert!(state_store_client.shutdown().await.is_ok());

            // exit_handle.try_exit().await.unwrap(); // TODO: uncomment once below race condition is fixed
            match exit_handle.try_exit().await {
                Ok(()) => Ok(()),
                Err(e) => {
                    match e {
                        azure_iot_operations_mqtt::session::SessionExitError::BrokerUnavailable { attempted } => {
                            // Because of a current race condition, we need to ignore this as it isn't indicative of a real error
                            if !attempted {
                                return Err(e.to_string());
                            }
                            Ok(())
                        },
                        _ => Err(e.to_string()),
                    }
                }
            }
        }
    });

    // if an assert fails in the test task, propagate the panic to end the test,
    // while still running the test task and the session to completion on the happy path
    assert!(tokio::try_join!(
        async move { test_task.await.map_err(|e| { e.to_string() }) },
        async move { session.run().await.map_err(|e| { e.to_string() }) }
    )
    .is_ok());
}
