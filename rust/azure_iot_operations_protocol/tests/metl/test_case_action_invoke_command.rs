// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use crate::metl::defaults::DefaultsType;
use crate::metl::test_case_duration::TestCaseDuration;

pub fn get_default_command_name<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_invoke_command) = default_action.invoke_command.as_ref() {
                if let Some(default_command_name) = default_invoke_command.command_name.as_ref() {
                    return Some(default_command_name.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_executor_id<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_invoke_command) = default_action.invoke_command.as_ref() {
                if let Some(default_executor_id) = default_invoke_command.executor_id.as_ref() {
                    return Some(default_executor_id.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_request_value<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_invoke_command) = default_action.invoke_command.as_ref() {
                if let Some(default_request_value) = default_invoke_command.request_value.as_ref() {
                    return Some(default_request_value.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_timeout<T: DefaultsType + Default>() -> Option<TestCaseDuration> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_action) = default_test_case.actions.as_ref() {
            if let Some(default_invoke_command) = default_action.invoke_command.as_ref() {
                if let Some(default_timeout) = default_invoke_command.timeout.as_ref() {
                    return Some((*default_timeout).clone());
                }
            }
        }
    }

    None
}
