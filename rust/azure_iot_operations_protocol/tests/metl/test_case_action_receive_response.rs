// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use crate::metl::defaults::DefaultsType;
use crate::metl::test_case_duration::TestCaseDuration;

pub fn get_default_topic<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_topic) = default_receive_response.topic.as_ref() {
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
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_payload) = default_receive_response.payload.as_ref() {
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
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_content_type) = default_receive_response.content_type.as_ref() {
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
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_format_indicator) = default_receive_response.format_indicator {
                    return Some(default_format_indicator);
                }
            }
        }
    }

    None
}

pub fn get_default_correlation_index<T: DefaultsType + Default>() -> Option<i32> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_correlation_index) = default_receive_response.correlation_index
                {
                    return Some(default_correlation_index);
                }
            }
        }
    }

    None
}

pub fn get_default_qos<T: DefaultsType + Default>() -> Option<i32> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_qos) = default_receive_response.qos {
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
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_message_expiry) =
                    default_receive_response.message_expiry.as_ref()
                {
                    return Some((*default_message_expiry).clone());
                }
            }
        }
    }

    None
}

pub fn get_default_status<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_status) = default_receive_response.status.as_ref() {
                    return Some(default_status.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_status_message<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_status_message) =
                    default_receive_response.status_message.as_ref()
                {
                    return Some(default_status_message.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_is_application_error<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_is_application_error) =
                    default_receive_response.is_application_error.as_ref()
                {
                    return Some(default_is_application_error.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_invalid_property_name<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_invalid_property_name) =
                    default_receive_response.invalid_property_name.as_ref()
                {
                    return Some(default_invalid_property_name.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_invalid_property_value<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_receive_response) = default_action.receive_response.as_ref() {
                if let Some(default_invalid_property_value) =
                    default_receive_response.invalid_property_value.as_ref()
                {
                    return Some(default_invalid_property_value.to_string());
                }
            }
        }
    }

    None
}
