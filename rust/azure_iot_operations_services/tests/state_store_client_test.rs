// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::Duration;

use azure_iot_operations_mqtt::session::{
    Session, SessionExitHandle, SessionOptionsBuilder, SessionPubReceiver, SessionPubSub,
};
use azure_iot_operations_mqtt::MqttConnectionSettingsBuilder;
use azure_iot_operations_protocol::common::hybrid_logical_clock::HybridLogicalClock;
use azure_iot_operations_services::state_store::{self, SetCondition, SetOptions};
use env_logger::Builder;

#[tokio::main(flavor = "current_thread")]
#[test]
async fn main() {
    Builder::new()
        .filter_level(log::LevelFilter::max())
        .format_timestamp(None)
        .filter_module("rumqttc", log::LevelFilter::Warn)
        .init();

    let connection_settings = MqttConnectionSettingsBuilder::default()
        .client_id("state-store-client-rust")
        .host_name("localhost")
        .tcp_port(1883u16)
        .keep_alive(Duration::from_secs(5))
        .use_tls(false)
        .build()
        .unwrap();

    let session_options = SessionOptionsBuilder::default()
        .connection_settings(connection_settings)
        .build()
        .unwrap();

    let mut session = Session::new(session_options).unwrap();
    let exit_handle = session.get_session_exit_handle();

    let state_store_client: state_store::Client<_, _> =
        state_store::Client::new(&mut session).unwrap();

    tokio::task::spawn(state_store_tests(state_store_client, exit_handle));

    session.run().await.unwrap();
}

// Tests these scenarios:
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
async fn state_store_tests(
    state_store_client: state_store::Client<SessionPubSub, SessionPubReceiver>,
    exit_handle: SessionExitHandle,
) {
    let timeout = Duration::from_secs(10);

    // ~~~~~~~~ Key 1 ~~~~~~~~
    // Test basic set and delete without fencing tokens or expiry
    let key1 = b"key1";
    let value1 = b"value1";

    // Delete key1 in case it was left over from a previous run
    let delete_cleanup_response = state_store_client
        .del(key1.to_vec(), None, timeout)
        .await
        .unwrap();
    log::info!("Delete key1: {:?}", delete_cleanup_response);

    // Tests 1 (valid new key/value with default setOptions), 6 (without fencing token where fencing_token not required)
    let set_new_key_value = state_store_client
        .set(
            key1.to_vec(),
            value1.to_vec(),
            timeout,
            None,
            SetOptions::default(),
        )
        .await
        .unwrap();
    assert!(set_new_key_value.response);
    log::info!("set_new_key_value response: {:?}", set_new_key_value);

    // Tests 2 (valid existing key/value with default setOptions)
    let set_existing_key_value = state_store_client
        .set(
            key1.to_vec(),
            b"value2".to_vec(),
            timeout,
            None,
            SetOptions::default(),
        )
        .await
        .unwrap();
    assert!(set_existing_key_value.response);
    log::info!(
        "set_existing_key_value response: {:?}",
        set_existing_key_value
    );

    // Tests 15 (where key exists), 19 (without fencing token where fencing_token not required)
    let delete_response = state_store_client
        .del(key1.to_vec(), None, timeout)
        .await
        .unwrap();
    assert_eq!(delete_response.response, 1);
    log::info!("Delete response: {:?}", delete_response);

    // ~~~~~~~~ Key 2 ~~~~~~~~
    // Tests where fencing token is required
    let key2 = b"key2";
    let mut key2_fencing_token = HybridLogicalClock::default();

    // Tests 5 (with fencing token where fencing_token not required), 7 (with SetOption.expires set)
    let set_fencing_token = state_store_client
        .set(
            key2.to_vec(),
            b"value3".to_vec(),
            timeout,
            Some(key2_fencing_token),
            SetOptions {
                expires: Some(Duration::from_secs(10)),
                ..Default::default()
            },
        )
        .await
        .unwrap();
    assert!(set_fencing_token.response);
    log::info!("set_fencing_token response: {:?}", set_fencing_token);
    key2_fencing_token = set_fencing_token.version.unwrap();

    // Tests 3 (with fencing token where fencing_token required)
    let value4 = b"value4";
    let set_fencing_token_required = state_store_client
        .set(
            key2.to_vec(),
            value4.to_vec(),
            timeout,
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
        "set_fencing_token_required response: {:?}",
        set_fencing_token_required
    );
    // save new version of fencing token
    key2_fencing_token = set_fencing_token_required.version.unwrap();

    // Tests 4 (without fencing token where fencing_token required (expect error))
    let set_missing_fencing_token = state_store_client
        .set(
            key2.to_vec(),
            b"value5".to_vec(),
            timeout,
            None,
            SetOptions::default(),
        )
        .await
        .expect_err("Expected error");
    log::info!(
        "set_missing_fencing_token response: {:?}",
        set_missing_fencing_token
    );

    // Tests 13 (where key exists), and also validates that `get` doesn't need fencing token
    let get_response = state_store_client
        .get(key2.to_vec(), timeout)
        .await
        .unwrap();
    log::info!("Get response: {:?}", get_response);
    if let Some(value) = get_response.response {
        assert_eq!(value, value4.to_vec());
    }

    // Tests 18 (without fencing token where fencing_token required (expect error))
    let delete_missing_fencing_token_response = state_store_client
        .del(key2.to_vec(), None, timeout)
        .await
        .expect_err("Expected error");
    log::info!(
        "delete_missing_fencing_token_response: {:?}",
        delete_missing_fencing_token_response
    );

    // Tests 24 (without fencing token where fencing_token required (expect error))
    let v_delete_missing_fencing_token_response = state_store_client
        .vdel(key2.to_vec(), value4.to_vec(), None, timeout)
        .await
        .expect_err("Expected error");
    log::info!(
        "v_delete_missing_fencing_token_response: {:?}",
        v_delete_missing_fencing_token_response
    );

    // Tests 15 (where key exists), 17 (with fencing token where fencing_token required)
    let delete_with_fencing_token_response = state_store_client
        .del(key2.to_vec(), Some(key2_fencing_token), timeout)
        .await
        .unwrap();
    assert_eq!(delete_with_fencing_token_response.response, 1);
    log::info!(
        "delete_with_fencing_token_response: {:?}",
        delete_with_fencing_token_response
    );

    // ~~~~~~~~ never key ~~~~~~~~
    // Tests scenarios where the key isn't found
    let never_key = b"never_key";
    let never_value = b"never_value";
    // Tests 14 (where key does not exist (expect success that indicates the key wasn't found))
    let get_no_key_response = state_store_client
        .get(never_key.to_vec(), timeout)
        .await
        .unwrap();
    assert!(get_no_key_response.response.is_none());
    log::info!("get_no_key_response: {:?}", get_no_key_response);

    // Tests 16 (where key does not exist (expect success that indicates 0 keys were deleted))
    let delete_no_key_response = state_store_client
        .del(never_key.to_vec(), None, timeout)
        .await
        .unwrap();
    assert_eq!(delete_no_key_response.response, 0);
    log::info!("delete_no_key_response: {:?}", delete_no_key_response);

    // Tests 21 (where key does not exist (expect success that indicates 0 keys were deleted))
    let v_delete_no_key_response = state_store_client
        .vdel(never_key.to_vec(), never_value.to_vec(), None, timeout)
        .await
        .unwrap();
    assert_eq!(v_delete_no_key_response.response, 0);
    log::info!("v_delete_no_key_response: {:?}", v_delete_no_key_response);

    // ~~~~~~~~ Key 3 ~~~~~~~~
    // Tests sets with various SetConditions
    let key3 = b"key3";

    // Tests 8 (with setCondition OnlyIfDoesNotExist and key doesn't exist)
    let set_if_not_exist = state_store_client
        .set(
            key3.to_vec(),
            value1.to_vec(),
            timeout,
            None,
            SetOptions {
                expires: Some(Duration::from_secs(10)),
                set_condition: SetCondition::OnlyIfDoesNotExist,
            },
        )
        .await
        .unwrap();
    assert!(set_if_not_exist.response);
    log::info!("set_if_not_exist response: {:?}", set_if_not_exist);

    // Tests 9 (with setCondition OnlyIfDoesNotExist and key exists (expect success that indicates the key wasn't set))
    let set_if_not_exist_fail = state_store_client
        .set(
            key3.to_vec(),
            value1.to_vec(),
            timeout,
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
        "set_if_not_exist_fail response: {:?}",
        set_if_not_exist_fail
    );

    // Tests 10 (with setCondition OnlyIfEqualOrDoesNotExist and key exists and is equal)
    let set_if_equal_or_not_exist_equal = state_store_client
        .set(
            key3.to_vec(),
            value1.to_vec(),
            timeout,
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
        "set_if_equal_or_not_exist_equal response: {:?}",
        set_if_equal_or_not_exist_equal
    );

    // Tests 11 (with setCondition OnlyIfEqualOrDoesNotExist and key exists and isn't equal (expect success that indicates the key wasn't set))
    let set_if_equal_or_not_exist_fail = state_store_client
        .set(
            key3.to_vec(),
            b"value2".to_vec(),
            timeout,
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
        "set_if_equal_or_not_exist_fail response: {:?}",
        set_if_equal_or_not_exist_fail
    );

    // Tests 25 (without fencing token where fencing_token not required)
    let v_delete_response_no_fencing_token = state_store_client
        .vdel(key3.to_vec(), value1.to_vec(), None, timeout)
        .await
        .unwrap();
    assert_eq!(v_delete_response_no_fencing_token.response, 1);
    log::info!(
        "v_delete_response_no_fencing_token response: {:?}",
        v_delete_response_no_fencing_token
    );

    // ~~~~~~~~ Key 4 ~~~~~~~~
    // Tests some other SetConditions
    let key4 = b"key4";
    let mut key4_fencing_token = HybridLogicalClock::default();

    // Tests 12 (with setCondition OnlyIfEqualOrDoesNotExist and key doesn't exist)
    let set_if_equal_or_not_exist_does_not_exist = state_store_client
        .set(
            key4.to_vec(),
            value1.to_vec(),
            timeout,
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
        "set_if_equal_or_not_exist_does_not_exist response: {:?}",
        set_if_equal_or_not_exist_does_not_exist
    );
    key4_fencing_token = set_if_equal_or_not_exist_does_not_exist.version.unwrap();

    // Tests 22 (where key exists and value doesn't match (expect success that indicates -1 keys were deleted))
    let v_delete_value_mismatch = state_store_client
        .vdel(
            key4.to_vec(),
            b"value2".to_vec(),
            Some(key4_fencing_token.clone()),
            timeout,
        )
        .await
        .unwrap();
    assert_eq!(v_delete_value_mismatch.response, -1);
    log::info!(
        "v_delete_value_mismatch response: {:?}",
        v_delete_value_mismatch
    );

    // Tests 26 (without fencing token where fencing_token required (expect error))
    let v_delete_response_missing_fencing_token = state_store_client
        .vdel(key4.to_vec(), value1.to_vec(), None, timeout)
        .await
        .expect_err("Expected error");
    log::info!(
        "v_delete_response_missing_fencing_token response: {:?}",
        v_delete_response_missing_fencing_token
    );

    // Tests 20 (where key exists and value matches), 23 (with fencing token where fencing_token required)
    let v_delete_response = state_store_client
        .vdel(
            key4.to_vec(),
            value1.to_vec(),
            Some(key4_fencing_token),
            timeout,
        )
        .await
        .unwrap();
    assert_eq!(v_delete_response.response, 1);
    log::info!("VDelete response: {:?}", v_delete_response);

    exit_handle.try_exit().await.unwrap();
}
