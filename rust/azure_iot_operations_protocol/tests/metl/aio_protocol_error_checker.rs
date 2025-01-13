// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use azure_iot_operations_protocol::common::aio_protocol_error::{AIOProtocolError, Value};
use iso8601_duration::Duration;

use crate::metl::test_case_catch;

pub fn check_error(
    test_case_catch: &test_case_catch::TestCaseCatch,
    aio_protocol_error: &AIOProtocolError,
) {
    assert_eq!(test_case_catch.get_error_kind(), aio_protocol_error.kind);

    if let Some(in_application) = test_case_catch.in_application {
        assert_eq!(in_application, aio_protocol_error.in_application);
    }

    if let Some(is_shallow) = test_case_catch.is_shallow {
        assert_eq!(is_shallow, aio_protocol_error.is_shallow);
    }

    if let Some(is_remote) = test_case_catch.is_remote {
        assert_eq!(is_remote, aio_protocol_error.is_remote);
    }

    if let Some(status_code) = test_case_catch.status_code {
        assert_eq!(status_code, aio_protocol_error.http_status_code);
    }

    if let Some(message) = test_case_catch.message.as_ref() {
        assert_eq!(message, aio_protocol_error.message.as_ref().unwrap());
    }

    if let Some(header_name) = test_case_catch
        .supplemental
        .get(test_case_catch::HEADER_NAME_KEY)
    {
        assert_eq!(header_name, &aio_protocol_error.header_name);
    }

    if let Some(header_value) = test_case_catch
        .supplemental
        .get(test_case_catch::HEADER_VALUE_KEY)
    {
        assert_eq!(header_value, &aio_protocol_error.header_value);
    }

    if let Some(timeout_name) = test_case_catch
        .supplemental
        .get(test_case_catch::TIMEOUT_NAME_KEY)
    {
        if let Some(expected_timeout_name) = timeout_name {
            if let Some(actual_timeout_name) = aio_protocol_error.timeout_name.as_ref() {
                assert_eq!(
                    expected_timeout_name,
                    &actual_timeout_name.replace('_', "").to_lowercase()
                );
            } else {
                panic!("no timeout_name value in AIOProtocolError");
            }
        } else {
            assert_eq!(None, aio_protocol_error.timeout_name);
        }
    }

    if let Some(timeout_value) = test_case_catch
        .supplemental
        .get(test_case_catch::TIMEOUT_VALUE_KEY)
    {
        if let Some(timeout_value) = timeout_value {
            assert_eq!(
                timeout_value.parse::<Duration>().unwrap().to_std(),
                aio_protocol_error.timeout_value
            );
        } else {
            assert_eq!(None, aio_protocol_error.timeout_value);
        }
    }

    if let Some(property_name) = test_case_catch
        .supplemental
        .get(test_case_catch::PROPERTY_NAME_KEY)
    {
        if let Some(expected_property_name) = property_name {
            if let Some(actual_property_name) = aio_protocol_error.property_name.as_ref() {
                assert_eq!(
                    expected_property_name,
                    &actual_property_name
                        .chars()
                        .skip(if let Some(ix) = actual_property_name.rfind('.') {
                            ix + 1
                        } else {
                            0
                        })
                        .collect::<String>()
                        .replace("__", ".")
                        .replace('_', "")
                        .replace('.', "__")
                        .to_lowercase()
                );
            } else {
                panic!("no property_name value in AIOProtocolError");
            }
        } else {
            assert_eq!(None, aio_protocol_error.property_name);
        }
    }

    if let Some(property_value) = test_case_catch
        .supplemental
        .get(test_case_catch::PROPERTY_VALUE_KEY)
    {
        if let Some(expected_value_string) = property_value {
            if let Some(actual_value) = aio_protocol_error.property_value.as_ref() {
                match actual_value {
                    Value::Integer(int_value) => {
                        assert_eq!(expected_value_string, &int_value.to_string());
                    }
                    Value::Float(float_value) => {
                        assert_eq!(expected_value_string, &float_value.to_string());
                    }
                    Value::String(string_value) => assert_eq!(expected_value_string, string_value),
                    Value::Boolean(bool_value) => {
                        assert_eq!(expected_value_string, &bool_value.to_string());
                    }
                };
            } else {
                panic!("no property_value value in AIOProtocolError");
            }
        } else {
            assert_eq!(None, aio_protocol_error.property_value);
        }
    }

    if let Some(command_name) = test_case_catch
        .supplemental
        .get(test_case_catch::COMMAND_NAME_KEY)
    {
        assert_eq!(command_name, &aio_protocol_error.command_name);
    }
}
