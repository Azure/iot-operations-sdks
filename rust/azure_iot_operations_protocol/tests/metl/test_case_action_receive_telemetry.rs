// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use crate::metl::defaults::DefaultsType;
use crate::metl::test_case_duration::TestCaseDuration;

pub fn get_default_topic<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_telemetry) = default_action.receive_telemetry.as_ref() {
                if let Some(default_topic) = default_receive_telemetry.topic.as_ref() {
                    return Some(default_topic.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_payload<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_telemetry) = default_action.receive_telemetry.as_ref() {
                if let Some(default_payload) = default_receive_telemetry.payload.as_ref() {
                    return Some(default_payload.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_content_type<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_telemetry) = default_action.receive_telemetry.as_ref() {
                if let Some(default_content_type) = default_receive_telemetry.content_type.as_ref()
                {
                    return Some(default_content_type.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_format_indicator<T: DefaultsType + Default>() -> Option<u8> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_telemetry) = default_action.receive_telemetry.as_ref() {
                if let Some(default_format_indicator) = default_receive_telemetry.format_indicator {
                    return Some(default_format_indicator);
                }
            }
        }
    }

    None
}

pub fn get_default_qos<T: DefaultsType + Default>() -> Option<i32> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_telemetry) = default_action.receive_telemetry.as_ref() {
                if let Some(default_qos) = default_receive_telemetry.qos {
                    return Some(default_qos);
                }
            }
        }
    }

    None
}

pub fn get_default_message_expiry<T: DefaultsType + Default>() -> Option<TestCaseDuration> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_telemetry) = default_action.receive_telemetry.as_ref() {
                if let Some(default_message_expiry) =
                    default_receive_telemetry.message_expiry.as_ref()
                {
                    return Some((*default_message_expiry).clone());
                }
            }
        }
    }

    None
}

pub fn get_default_source_index<T: DefaultsType + Default>() -> Option<i32> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_telemetry) = default_action.receive_telemetry.as_ref() {
                if let Some(default_source_index) = default_receive_telemetry.source_index {
                    return Some(default_source_index);
                }
            }
        }
    }

    None
}
