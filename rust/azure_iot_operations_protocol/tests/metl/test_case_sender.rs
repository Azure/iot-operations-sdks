// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::marker::PhantomData;

use serde::Deserialize;

use crate::metl::defaults::DefaultsType;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseSender<T: DefaultsType + Default> {
    #[serde(default)]
    pub defaults_type: PhantomData<T>,

    #[serde(rename = "telemetry-name")]
    #[serde(default = "get_default_telemetry_name::<T>")]
    pub telemetry_name: Option<String>,

    #[serde(rename = "telemetry-topic")]
    #[serde(default = "get_default_telemetry_topic::<T>")]
    pub telemetry_topic: Option<String>,

    #[serde(rename = "model-id")]
    #[serde(default = "get_default_model_id::<T>")]
    pub model_id: Option<String>,

    #[serde(rename = "data-schema")]
    #[serde(default = "get_default_data_schema::<T>")]
    pub data_schema: Option<String>,

    #[serde(rename = "topic-namespace")]
    #[serde(default = "get_default_topic_namespace::<T>")]
    pub topic_namespace: Option<String>,

    #[serde(rename = "custom-token-map")]
    pub custom_token_map: Option<HashMap<String, String>>,
}

pub fn get_default_telemetry_name<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_sender) = default_prologue.sender.as_ref() {
                if let Some(default_telemetry_name) = default_sender.telemetry_name.as_ref() {
                    return Some(default_telemetry_name.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_telemetry_topic<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_sender) = default_prologue.sender.as_ref() {
                if let Some(default_telemetry_topic) = default_sender.telemetry_topic.as_ref() {
                    return Some(default_telemetry_topic.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_model_id<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_sender) = default_prologue.sender.as_ref() {
                if let Some(default_model_id) = default_sender.model_id.as_ref() {
                    return Some(default_model_id.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_data_schema<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_sender) = default_prologue.sender.as_ref() {
                if let Some(default_data_schema) = default_sender.data_schema.as_ref() {
                    return Some(default_data_schema.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_topic_namespace<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_sender) = default_prologue.sender.as_ref() {
                if let Some(default_topic_namespace) = default_sender.topic_namespace.as_ref() {
                    return Some(default_topic_namespace.to_string());
                }
            }
        }
    }

    None
}
