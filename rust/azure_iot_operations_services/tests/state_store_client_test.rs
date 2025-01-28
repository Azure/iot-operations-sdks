// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#![cfg(feature = "state_store")]

use std::{env, time::Duration};

use env_logger::Builder;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionManagedClient, SessionOptionsBuilder,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::application::{
    ApplicationContext, ApplicationContextOptionsBuilder,
};
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;
use azure_iot_operations_services::state_store::{self, SetCondition, SetOptions};

// These tests test these scenarios - numbers are linked inline:
// SET
//    1. valid new key/value with default setOptions
//    2. valid existing key/value with default setOptions
//    3. with fencing token where fencing_token required
//    4. without fencing token where fencing_token required (expect error)
//    5. with fencing token where fencing_token not required
//    6. without fencing token where fencing_token not required
//    7. with SetOption.expires set
//    8. with setCondition OnlyIfDoesNotExist and key doesn't exist
//    9. with setCondition OnlyIfDoesNotExist and key exists (expect success that indicates the key wasn't set)
//    10. with setCondition OnlyIfEqualOrDoesNotExist and key exists and is equal
//    11. with setCondition OnlyIfEqualOrDoesNotExist and key exists and isn't equal (expect success that indicates the key wasn't set)
//    12. with setCondition OnlyIfEqualOrDoesNotExist and key doesn't exist
// GET
//    13. where key exists
//    14. where key does not exist (expect success that indicates the key wasn't found)
// DEL
//    15. where key exists
//    16. where key does not exist (expect success that indicates 0 keys were deleted)
//    17. with fencing token where fencing_token required
//    18. without fencing token where fencing_token required (expect error)
//    19. without fencing token where fencing_token not required
// VDEL
//    20. where key exists and value matches
//    21. where key does not exist (expect success that indicates 0 keys were deleted)
//    22. where key exists and value doesn't match (expect success that indicates -1 keys were deleted
//    23. with fencing token where fencing_token required
//    24. without fencing token where fencing_token required (expect error)
//    25. without fencing token where fencing_token not required
// OBSERVE
//    26. where key exists
//    27. where key does not exist (success looks the same as if the key exists)
//    28. where key is already being observed (error returned)
//    29. where key is already being observed, but the KeyObservation has been dropped (successful)
//    30. where key was observed, unobserved, and then observed again (successful)
// UNOBSERVE
//    31. where key is being observed
//    32. where key was not being observed (expect success that indicates the key wasn't being observed)
// KEY NOTIFICATION
//    33. 1 set(v1) notification received after observe and then key is set(V1)
//    34. 1 del notification received after observe and then key is del
//    35. 1 set(v2), 1 del, 1 set(v3) notifications received after set(v1), del, observe, set(v2), del, set(v3), unobserve, set(v4), del. This test is confirming that operations that happen outside of the observation aren't received.
//    36. TODO set with key expiry, recv delete notification once key expires

const VALUE1: &[u8] = b"value1";
const VALUE2: &[u8] = b"value2";
const VALUE3: &[u8] = b"value3";
const VALUE4: &[u8] = b"value4";
const TIMEOUT: Duration = Duration::from_secs(10);

fn setup_test(
    client_id: &str,
) -> Result<
    (
        Session,
        state_store::Client<SessionManagedClient>,
        SessionExitHandle,
    ),
    (),
> {
    let _ = Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .filter_module("azure_iot_operations", log::LevelFilter::Warn)
        .try_init();
    if env::var("ENABLE_NETWORK_TESTS").is_err() {
        log::warn!("This test is skipped. Set ENABLE_NETWORK_TESTS to run.");
        return Err(());
    }

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
    let application_context =
        ApplicationContext::new(ApplicationContextOptionsBuilder::default().build().unwrap());

    let state_store_client = state_store::Client::new(
        application_context,
        session.create_managed_client(),
        state_store::ClientOptionsBuilder::default()
            .build()
            .unwrap(),
    )
    .unwrap();
    let exit_handle: SessionExitHandle = session.create_exit_handle();
    Ok((session, state_store_client, exit_handle))
}

/// ~~~~~~~~ Key 1 ~~~~~~~~
/// Tests basic set and delete without fencing tokens or expiry
#[tokio::test]
async fn state_store_basic_set_delete_network_tests() {
    let log_identifier = "basic_set_delete";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_basic_set_delete_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let key1 = b"key1";

            // Delete key1 in case it was left over from a previous run
            let delete_cleanup_response = state_store_client
                .del(key1.to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] Delete key1: {:?}",
                delete_cleanup_response
            );

            // Tests 1 (valid new key/value with default setOptions), 6 (without fencing token where fencing_token not required)
            let set_new_key_value = state_store_client
                .set(
                    key1.to_vec(),
                    VALUE1.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions::default(),
                )
                .await
                .unwrap();
            assert!(set_new_key_value.response);
            log::info!(
                "[{log_identifier}] set_new_key_value response: {:?}",
                set_new_key_value
            );

            // Tests 2 (valid existing key/value with default setOptions)
            let set_existing_key_value = state_store_client
                .set(
                    key1.to_vec(),
                    VALUE2.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions::default(),
                )
                .await
                .unwrap();
            assert!(set_existing_key_value.response);
            log::info!(
                "[{log_identifier}] set_existing_key_value response: {:?}",
                set_existing_key_value
            );

            // Tests 15 (where key exists), 19 (without fencing token where fencing_token not required)
            let delete_response = state_store_client
                .del(key1.to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            assert_eq!(delete_response.response, 1);
            log::info!("[{log_identifier}] Delete response: {:?}", delete_response);

            // Shutdown state store client and underlying resources
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

/// ~~~~~~~~ Key 2 ~~~~~~~~
/// Tests where fencing token is required
#[tokio::test]
async fn state_store_fencing_token_network_tests() {
    let log_identifier = "fencing_token";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_fencing_token_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let key2 = b"key2";
            let mut key2_fencing_token = HybridLogicalClock::default();

            // Tests 5 (with fencing token where fencing_token not required), 7 (with SetOption.expires set)
            let set_fencing_token = state_store_client
                .set(
                    key2.to_vec(),
                    VALUE3.to_vec(),
                    TIMEOUT,
                    Some(key2_fencing_token),
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        ..Default::default()
                    },
                )
                .await
                .unwrap();
            assert!(set_fencing_token.response);
            log::info!(
                "[{log_identifier}] set_fencing_token response: {:?}",
                set_fencing_token
            );
            key2_fencing_token = set_fencing_token.version.unwrap();

            // Tests 3 (with fencing token where fencing_token required)
            let set_fencing_token_required = state_store_client
                .set(
                    key2.to_vec(),
                    VALUE4.to_vec(),
                    TIMEOUT,
                    Some(key2_fencing_token),
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        ..Default::default()
                    },
                )
                .await
                .unwrap();
            assert!(set_fencing_token_required.response);
            log::info!(
                "[{log_identifier}] set_fencing_token_required response: {:?}",
                set_fencing_token_required
            );
            // save new version of fencing token
            key2_fencing_token = set_fencing_token_required.version.unwrap();

            // Tests 4 (without fencing token where fencing_token required (expect error))
            let set_missing_fencing_token = state_store_client
                .set(
                    key2.to_vec(),
                    b"value5".to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions::default(),
                )
                .await
                .expect_err("Expected error");
            log::info!(
                "[{log_identifier}] set_missing_fencing_token response: {:?}",
                set_missing_fencing_token
            );

            // Tests 13 (where key exists), and also validates that `get` doesn't need fencing token
            let get_response = state_store_client
                .get(key2.to_vec(), TIMEOUT)
                .await
                .unwrap();
            log::info!("[{log_identifier}] Get response: {:?}", get_response);
            if let Some(value) = get_response.response {
                assert_eq!(value, VALUE4.to_vec());
            }

            // Tests 18 (without fencing token where fencing_token required (expect error))
            let delete_missing_fencing_token_response = state_store_client
                .del(key2.to_vec(), None, TIMEOUT)
                .await
                .expect_err("Expected error");
            log::info!(
                "[{log_identifier}] delete_missing_fencing_token_response: {:?}",
                delete_missing_fencing_token_response
            );

            // Tests 24 (without fencing token where fencing_token required (expect error))
            let v_delete_missing_fencing_token_response = state_store_client
                .vdel(key2.to_vec(), VALUE4.to_vec(), None, TIMEOUT)
                .await
                .expect_err("Expected error");
            log::info!(
                "[{log_identifier}] v_delete_missing_fencing_token_response: {:?}",
                v_delete_missing_fencing_token_response
            );

            // Tests 15 (where key exists), 17 (with fencing token where fencing_token required)
            let delete_with_fencing_token_response = state_store_client
                .del(key2.to_vec(), Some(key2_fencing_token), TIMEOUT)
                .await
                .unwrap();
            assert_eq!(delete_with_fencing_token_response.response, 1);
            log::info!(
                "[{log_identifier}] delete_with_fencing_token_response: {:?}",
                delete_with_fencing_token_response
            );

            // Shutdown state store client and underlying resources
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

/// ~~~~~~~~ never key ~~~~~~~~
/// Tests scenarios where the key isn't found
#[tokio::test]
async fn state_store_key_not_found_network_tests() {
    let log_identifier = "key_not_found";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_key_not_found_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let never_key = b"never_key";
            // Tests 14 (where key does not exist (expect success that indicates the key wasn't found))
            let get_no_key_response = state_store_client
                .get(never_key.to_vec(), TIMEOUT)
                .await
                .unwrap();
            assert!(get_no_key_response.response.is_none());
            log::info!(
                "[{log_identifier}] get_no_key_response: {:?}",
                get_no_key_response
            );

            // Tests 16 (where key does not exist (expect success that indicates 0 keys were deleted))
            let delete_no_key_response = state_store_client
                .del(never_key.to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            assert_eq!(delete_no_key_response.response, 0);
            log::info!(
                "[{log_identifier}] delete_no_key_response: {:?}",
                delete_no_key_response
            );

            // Tests 21 (where key does not exist (expect success that indicates 0 keys were deleted))
            let v_delete_no_key_response = state_store_client
                .vdel(never_key.to_vec(), b"never_value".to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            assert_eq!(v_delete_no_key_response.response, 0);
            log::info!(
                "[{log_identifier}] v_delete_no_key_response: {:?}",
                v_delete_no_key_response
            );

            // Shutdown state store client and underlying resources
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

/// ~~~~~~~~ Key 3 ~~~~~~~~
/// Tests sets with various SetConditions
#[tokio::test]
async fn state_store_set_conditions_network_tests() {
    let log_identifier = "set_conditions";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_set_conditions_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let key3 = b"key3";

            // Tests 8 (with setCondition OnlyIfDoesNotExist and key doesn't exist)
            let set_if_not_exist = state_store_client
                .set(
                    key3.to_vec(),
                    VALUE1.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        set_condition: SetCondition::OnlyIfDoesNotExist,
                    },
                )
                .await
                .unwrap();
            assert!(set_if_not_exist.response);
            log::info!(
                "[{log_identifier}] set_if_not_exist response: {:?}",
                set_if_not_exist
            );

            // Tests 9 (with setCondition OnlyIfDoesNotExist and key exists (expect success that indicates the key wasn't set))
            let set_if_not_exist_fail = state_store_client
                .set(
                    key3.to_vec(),
                    VALUE1.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        set_condition: SetCondition::OnlyIfDoesNotExist,
                    },
                )
                .await
                .unwrap();
            assert!(!set_if_not_exist_fail.response);
            log::info!(
                "[{log_identifier}] set_if_not_exist_fail response: {:?}",
                set_if_not_exist_fail
            );

            // Tests 10 (with setCondition OnlyIfEqualOrDoesNotExist and key exists and is equal)
            let set_if_equal_or_not_exist_equal = state_store_client
                .set(
                    key3.to_vec(),
                    VALUE1.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        set_condition: SetCondition::OnlyIfEqualOrDoesNotExist,
                    },
                )
                .await
                .unwrap();
            assert!(set_if_equal_or_not_exist_equal.response);
            log::info!(
                "[{log_identifier}] set_if_equal_or_not_exist_equal response: {:?}",
                set_if_equal_or_not_exist_equal
            );

            // Tests 11 (with setCondition OnlyIfEqualOrDoesNotExist and key exists and isn't equal (expect success that indicates the key wasn't set))
            let set_if_equal_or_not_exist_fail = state_store_client
                .set(
                    key3.to_vec(),
                    VALUE2.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        set_condition: SetCondition::OnlyIfEqualOrDoesNotExist,
                    },
                )
                .await
                .unwrap();
            assert!(!set_if_equal_or_not_exist_fail.response);
            log::info!(
                "[{log_identifier}] set_if_equal_or_not_exist_fail response: {:?}",
                set_if_equal_or_not_exist_fail
            );

            // Tests 25 (without fencing token where fencing_token not required)
            let v_delete_response_no_fencing_token = state_store_client
                .vdel(key3.to_vec(), VALUE1.to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            assert_eq!(v_delete_response_no_fencing_token.response, 1);
            log::info!(
                "[{log_identifier}] v_delete_response_no_fencing_token response: {:?}",
                v_delete_response_no_fencing_token
            );

            // Shutdown state store client and underlying resources
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

/// ~~~~~~~~ Key 4 ~~~~~~~~
/// Tests some other SetConditions
#[tokio::test]
async fn state_store_key_set_conditions_2_network_tests() {
    let log_identifier = "set_conditions_2";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_set_conditions_2_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let key4 = b"key4";
            let mut key4_fencing_token = HybridLogicalClock::default();

            // Tests 12 (with setCondition OnlyIfEqualOrDoesNotExist and key doesn't exist)
            let set_if_equal_or_not_exist_does_not_exist = state_store_client
                .set(
                    key4.to_vec(),
                    VALUE1.to_vec(),
                    TIMEOUT,
                    Some(key4_fencing_token),
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        set_condition: SetCondition::OnlyIfEqualOrDoesNotExist,
                    },
                )
                .await
                .unwrap();
            assert!(set_if_equal_or_not_exist_does_not_exist.response);
            log::info!(
                "[{log_identifier}] set_if_equal_or_not_exist_does_not_exist response: {:?}",
                set_if_equal_or_not_exist_does_not_exist
            );
            key4_fencing_token = set_if_equal_or_not_exist_does_not_exist.version.unwrap();

            // Tests 22 (where key exists and value doesn't match (expect success that indicates -1 keys were deleted))
            let v_delete_value_mismatch = state_store_client
                .vdel(
                    key4.to_vec(),
                    VALUE2.to_vec(),
                    Some(key4_fencing_token.clone()),
                    TIMEOUT,
                )
                .await
                .unwrap();
            assert_eq!(v_delete_value_mismatch.response, -1);
            log::info!(
                "[{log_identifier}] v_delete_value_mismatch response: {:?}",
                v_delete_value_mismatch
            );

            // Tests 20 (where key exists and value matches), 23 (with fencing token where fencing_token required)
            let v_delete_response = state_store_client
                .vdel(
                    key4.to_vec(),
                    VALUE1.to_vec(),
                    Some(key4_fencing_token),
                    TIMEOUT,
                )
                .await
                .unwrap();
            assert_eq!(v_delete_response.response, 1);
            log::info!(
                "[{log_identifier}] VDelete response: {:?}",
                v_delete_response
            );

            // Shutdown state store client and underlying resources
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

/// ~~~~~~~~ Key 5 ~~~~~~~~
/// Tests basic recv set notification, as well as basic observe (where key doesn't exist) and unobserve
#[tokio::test]
async fn state_store_set_key_notifications_network_tests() {
    let log_identifier = "set_key_notifications";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_set_key_notifications_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let key5 = b"key5";

            // Tests 27 (where key does not exist (success))
            let mut observe_no_key = state_store_client
                .observe(key5.to_vec(), TIMEOUT)
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] observe_no_key response: {:?}",
                observe_no_key
            );
            let receive_notifications_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    // Tests 33 (1 set(v1) notification received after observe and then key is set(V1))
                    while let Some((notification, _)) =
                        observe_no_key.response.recv_notification().await
                    {
                        count += 1;
                        log::info!("[{log_identifier}] Notification: {:?}", notification);
                        assert_eq!(notification.key, key5);
                        assert_eq!(
                            notification.operation,
                            state_store::Operation::Set(VALUE1.to_vec())
                        );
                        // if something weird happens, this should prevent an infinite loop.
                        // Note that this does prevent getting an accurate count of how many extra unexpected notifications were received
                        assert!(count < 2);
                    }
                    // only one set notification should occur
                    assert_eq!(count, 1);
                    log::info!("[{log_identifier}] Notification receiver closed");
                }
            });
            let set_for_notification = state_store_client
                .set(
                    key5.to_vec(),
                    VALUE1.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        ..Default::default()
                    },
                )
                .await
                .unwrap();
            assert!(set_for_notification.response);
            log::info!(
                "[{log_identifier}] set_for_notification response: {:?}",
                set_for_notification
            );

            // Tests 31 (where key is being observed)
            let unobserve_where_observed = state_store_client
                .unobserve(key5.to_vec(), TIMEOUT)
                .await
                .unwrap();
            assert!(unobserve_where_observed.response);
            log::info!(
                "[{log_identifier}] unobserve_where_observed response: {:?}",
                unobserve_where_observed
            );

            // wait for the receive_notifications_task to finish to ensure any failed asserts are captured.
            assert!(receive_notifications_task.await.is_ok());
            // Shutdown state store client and underlying resources
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

/// ~~~~~~~~ Key 6 ~~~~~~~~
/// basic recv del notification, as well as basic observe (where key does exist) and unobserve
#[tokio::test]
async fn state_store_del_key_notifications_network_tests() {
    let log_identifier = "del_key_notifications";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_del_key_notifications_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let key6 = b"key6";
            let set_for_key6_notification = state_store_client
                .set(
                    key6.to_vec(),
                    VALUE1.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        ..Default::default()
                    },
                )
                .await
                .unwrap();
            assert!(set_for_key6_notification.response);
            log::info!(
                "[{log_identifier}] set_for_key6_notification response: {:?}",
                set_for_key6_notification
            );

            // Tests 26 (where key exists)
            let mut observe_key = state_store_client
                .observe(key6.to_vec(), TIMEOUT)
                .await
                .unwrap();
            log::info!("[{log_identifier}] observe_key response: {:?}", observe_key);
            let receive_notifications_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    // Tests 34 (1 del notification received after observe and then key is del)
                    while let Some((notification, _)) =
                        observe_key.response.recv_notification().await
                    {
                        count += 1;
                        log::info!("[{log_identifier}] Notification: {:?}", notification);
                        assert_eq!(notification.key, key6);
                        assert_eq!(notification.operation, state_store::Operation::Del);
                        // if something weird happens, this should prevent an infinite loop.
                        // Note that this does prevent getting an accurate count of how many extra unexpected notifications were received
                        assert!(count < 2);
                    }
                    // only one del notification should occur
                    assert_eq!(count, 1);
                    log::info!("[{log_identifier}] Notification receiver closed");
                }
            });
            let del_for_notification = state_store_client
                .del(key6.to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            assert_eq!(del_for_notification.response, 1);
            log::info!(
                "[{log_identifier}] del_for_notification response: {:?}",
                del_for_notification
            );

            // Tests 31 (where key is being observed)
            let unobserve_key6 = state_store_client
                .unobserve(key6.to_vec(), TIMEOUT)
                .await
                .unwrap();
            assert!(unobserve_key6.response);
            log::info!(
                "[{log_identifier}] unobserve_key6 response: {:?}",
                unobserve_key6
            );

            // wait for the receive_notifications_task to finish to ensure any failed asserts are captured.
            assert!(receive_notifications_task.await.is_ok());
            // Shutdown state store client and underlying resources
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

/// ~~~~~~~~ Key 7 ~~~~~~~~
/// testing observe and unobserve scenarios around being or not being observed and still having or not having references to being observed
#[tokio::test]
async fn state_store_observe_unobserve_network_tests() {
    let log_identifier = "observe_unobserve";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_observe_unobserve_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            let key7 = b"key7";

            // Tests 32 (where key was not being observed (expect success that indicates the key wasn't being observed))
            let unobserve_no_observe = state_store_client
                .unobserve(key7.to_vec(), TIMEOUT)
                .await
                .unwrap();
            assert!(!unobserve_no_observe.response);
            log::info!(
                "[{log_identifier}] unobserve_no_observe response: {:?}",
                unobserve_no_observe
            );

            // Tests 27 (where key does not exist (success))
            let observe_key7 = state_store_client
                .observe(key7.to_vec(), TIMEOUT)
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] observe_key7 response: {:?}",
                observe_key7
            );

            // Tests 28 (where key is already being observed (error returned))
            let double_observe_key7 = state_store_client
                .observe(key7.to_vec(), TIMEOUT)
                .await
                .expect_err("Expected error");
            log::info!(
                "[{log_identifier}] double_observe_key7 response: {:?}",
                double_observe_key7
            );

            // drop KeyObservation
            drop(observe_key7.response);

            // Tests 29 (where key is already being observed, but the KeyObservation has been dropped (successful))
            let observe_key7_after_drop = state_store_client
                .observe(key7.to_vec(), TIMEOUT)
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] observe_key7_after_drop response: {:?}",
                observe_key7_after_drop
            );

            let unobserve_key7 = state_store_client
                .unobserve(key7.to_vec(), TIMEOUT)
                .await
                .unwrap();
            assert!(unobserve_key7.response);
            log::info!(
                "[{log_identifier}] unobserve_key7 response: {:?}",
                unobserve_key7
            );

            // Tests 30 (where key was observed, unobserved, and then observed again (successful))
            let observe_key7_after_unobserve = state_store_client
                .observe(key7.to_vec(), TIMEOUT)
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] observe_key7_after_unobserve response: {:?}",
                observe_key7_after_unobserve
            );

            // clean up
            let unobserve_key7_cleanup = state_store_client
                .unobserve(key7.to_vec(), TIMEOUT)
                .await
                .unwrap();
            assert!(unobserve_key7_cleanup.response);
            log::info!(
                "[{log_identifier}] unobserve_key7_cleanup response: {:?}",
                unobserve_key7_cleanup
            );

            // Shutdown state store client and underlying resources
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

/// ~~~~~~~~ Key 8 ~~~~~~~~
/// complicated recv scenario checking for the right number of notifications
/// Tests 35 (1 set(v2), 1 del, 1 set(v3) notifications received after set(v1), del, observe, set(v2), del, set(v3), unobserve, set(v4), del. This test is confirming that operations that happen outside of the observation aren't received.)
#[tokio::test]
async fn state_store_complicated_recv_key_notifications_network_tests() {
    let log_identifier = "complicated_recv_key_notifications";
    let Ok((mut session, state_store_client, exit_handle)) =
        setup_test("state_store_complicated_recv_key_notifications_network_tests-rust")
    else {
        // Network tests disabled, skipping tests
        return;
    };

    let test_task = tokio::task::spawn({
        async move {
            // Tests 35 (1 set(v2), 1 del, 1 set(v3) notifications received after set(v1), del, observe, set(v2), del, set(v3), unobserve, set(v4), del. This test is confirming that operations that happen outside of the observation aren't received.)
            let key8 = b"key8";

            let set_for_key8_notification = state_store_client
                .set(
                    key8.to_vec(),
                    VALUE1.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        ..Default::default()
                    },
                )
                .await
                .unwrap();
            assert!(set_for_key8_notification.response);
            log::info!(
                "[{log_identifier}] set_for_key8_notification response: {:?}",
                set_for_key8_notification
            );

            let del_for_key8_notification = state_store_client
                .del(key8.to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            assert_eq!(del_for_key8_notification.response, 1);
            log::info!(
                "[{log_identifier}] del_for_key8_notification response: {:?}",
                del_for_key8_notification
            );

            let mut observe_key8 = state_store_client
                .observe(key8.to_vec(), TIMEOUT)
                .await
                .unwrap();
            log::info!(
                "[{log_identifier}] observe_key8 response: {:?}",
                observe_key8
            );
            let receive_notifications_task = tokio::task::spawn({
                async move {
                    let mut count = 0;
                    if let Some((notification, _)) = observe_key8.response.recv_notification().await
                    {
                        count += 1;
                        log::info!("[{log_identifier}] Notification: {:?}", notification);
                        assert_eq!(notification.key, key8);
                        assert_eq!(
                            notification.operation,
                            state_store::Operation::Set(VALUE2.to_vec())
                        );
                    }
                    if let Some((notification, _)) = observe_key8.response.recv_notification().await
                    {
                        count += 1;
                        log::info!("[{log_identifier}] Notification: {:?}", notification);
                        assert_eq!(notification.key, key8);
                        assert_eq!(notification.operation, state_store::Operation::Del);
                    }
                    if let Some((notification, _)) = observe_key8.response.recv_notification().await
                    {
                        count += 1;
                        log::info!("[{log_identifier}] Notification: {:?}", notification);
                        assert_eq!(notification.key, key8);
                        assert_eq!(
                            notification.operation,
                            state_store::Operation::Set(VALUE3.to_vec())
                        );
                    }
                    while let Some((notification, _)) =
                        observe_key8.response.recv_notification().await
                    {
                        count += 1;
                        log::error!("[{log_identifier}] Unexpected: {:?}", notification);
                        // if something weird happens, this should prevent an infinite loop.
                        // Note that this does prevent getting an accurate count of how many extra unexpected notifications were received
                        assert!(count < 4);
                    }
                    // only the 3 expected notifications should occur
                    assert_eq!(count, 3);
                    log::info!("[{log_identifier}] Notification receiver closed");
                }
            });
            let set_key8_value2 = state_store_client
                .set(
                    key8.to_vec(),
                    VALUE2.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        ..Default::default()
                    },
                )
                .await
                .unwrap();
            assert!(set_key8_value2.response);
            log::info!(
                "[{log_identifier}] set_key8_value2 response: {:?}",
                set_key8_value2
            );

            let del_key8 = state_store_client
                .del(key8.to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            assert_eq!(del_key8.response, 1);
            log::info!("[{log_identifier}] del_key8 response: {:?}", del_key8);

            let set_key8_value3 = state_store_client
                .set(
                    key8.to_vec(),
                    VALUE3.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        ..Default::default()
                    },
                )
                .await
                .unwrap();
            assert!(set_key8_value3.response);
            log::info!(
                "[{log_identifier}] set_key8_value3 response: {:?}",
                set_key8_value3
            );

            // Tests 31 (where key is being observed)
            let unobserve_key8 = state_store_client
                .unobserve(key8.to_vec(), TIMEOUT)
                .await
                .unwrap();
            assert!(unobserve_key8.response);
            log::info!(
                "[{log_identifier}] unobserve_key8 response: {:?}",
                unobserve_key8
            );

            let set_key8_no_notification = state_store_client
                .set(
                    key8.to_vec(),
                    VALUE4.to_vec(),
                    TIMEOUT,
                    None,
                    SetOptions {
                        expires: Some(Duration::from_secs(10)),
                        ..Default::default()
                    },
                )
                .await
                .unwrap();
            assert!(set_key8_no_notification.response);
            log::info!(
                "[{log_identifier}] set_key8_no_notification response: {:?}",
                set_key8_no_notification
            );

            let del_key8_no_notification = state_store_client
                .del(key8.to_vec(), None, TIMEOUT)
                .await
                .unwrap();
            assert_eq!(del_key8_no_notification.response, 1);
            log::info!(
                "[{log_identifier}] del_key8_no_notification response: {:?}",
                del_key8_no_notification
            );

            // wait for the receive_notifications_task to finish to ensure any failed asserts are captured.
            assert!(receive_notifications_task.await.is_ok());
            // Shutdown state store client and underlying resources
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
