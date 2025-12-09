// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use crate::metl::defaults::DefaultsType;
use crate::metl::test_case_duration::TestCaseDuration;

pub fn get_default_telemetry_name<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_send_telemetry) = default_action.send_telemetry.as_ref() {
                if let Some(default_telemetry_name) = default_send_telemetry.telemetry_name.as_ref()
                {
                    return Some(default_telemetry_name.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_timeout<T: DefaultsType + Default>() -> Option<TestCaseDuration> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_send_telemetry) = default_action.send_telemetry.as_ref() {
                if let Some(default_timeout) = default_send_telemetry.timeout.as_ref() {
                    return Some((*default_timeout).clone());
                }
            }
        }
    }

    None
}

pub fn get_default_telemetry_value<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_send_telemetry) = default_action.send_telemetry.as_ref() {
                if let Some(default_telemetry_value) =
                    default_send_telemetry.telemetry_value.as_ref()
                {
                    return Some(default_telemetry_value.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_qos<T: DefaultsType + Default>() -> Option<i32> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_send_telemetry) = default_action.send_telemetry.as_ref() {
                if let Some(default_qos) = default_send_telemetry.qos {
                    return Some(default_qos);
                }
            }
        }
    }

    None
}
