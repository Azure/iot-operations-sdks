// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::marker::PhantomData;

use serde::Deserialize;

use crate::metl::defaults::DefaultsType;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseInvoker<T: DefaultsType + Default> {
    #[serde(default)]
    pub defaults_type: PhantomData<T>,

    #[serde(rename = "command-name")]
    #[serde(default = "get_default_command_name::<T>")]
    pub command_name: Option<String>,

    #[serde(rename = "request-topic")]
    #[serde(default = "get_default_request_topic::<T>")]
    pub request_topic: Option<String>,

    #[serde(rename = "topic-namespace")]
    #[serde(default = "get_default_topic_namespace::<T>")]
    pub topic_namespace: Option<String>,

    #[serde(rename = "response-topic-prefix")]
    #[serde(default = "get_default_response_topic_prefix::<T>")]
    pub response_topic_prefix: Option<String>,

    #[serde(rename = "response-topic-suffix")]
    #[serde(default = "get_default_response_topic_suffix::<T>")]
    pub response_topic_suffix: Option<String>,

    #[serde(rename = "topic-token-map")]
    pub topic_token_map: Option<HashMap<String, String>>,

    #[serde(rename = "response-topic-map")]
    pub response_topic_map: Option<HashMap<String, Option<String>>>,
}

pub fn get_default_command_name<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_invoker) = default_prologue.invoker.as_ref() {
                if let Some(default_command_name) = default_invoker.command_name.as_ref() {
                    return Some(default_command_name.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_request_topic<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_invoker) = default_prologue.invoker.as_ref() {
                if let Some(default_request_topic) = default_invoker.request_topic.as_ref() {
                    return Some(default_request_topic.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_topic_namespace<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_invoker) = default_prologue.invoker.as_ref() {
                if let Some(default_topic_namespace) = default_invoker.topic_namespace.as_ref() {
                    return Some(default_topic_namespace.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_response_topic_prefix<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_invoker) = default_prologue.invoker.as_ref() {
                if let Some(default_response_topic_prefix) =
                    default_invoker.response_topic_prefix.as_ref()
                {
                    return Some(default_response_topic_prefix.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_response_topic_suffix<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_invoker) = default_prologue.invoker.as_ref() {
                if let Some(default_response_topic_suffix) =
                    default_invoker.response_topic_suffix.as_ref()
                {
                    return Some(default_response_topic_suffix.to_string());
                }
            }
        }
    }

    None
}
