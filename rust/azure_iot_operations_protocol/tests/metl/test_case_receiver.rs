// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::marker::PhantomData;

use serde::Deserialize;

use crate::metl::defaults::DefaultsType;
use crate::metl::test_case_error::TestCaseError;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseReceiver<T: DefaultsType + Default> {
    #[serde(default)]
    pub defaults_type: PhantomData<T>,

    #[serde(rename = "telemetry-topic")]
    #[serde(default = "get_default_telemetry_topic::<T>")]
    pub telemetry_topic: Option<String>,

    #[serde(rename = "topic-namespace")]
    #[serde(default = "get_default_topic_namespace::<T>")]
    pub topic_namespace: Option<String>,

    #[serde(rename = "topic-token-map")]
    pub topic_token_map: Option<HashMap<String, String>>,

    #[serde(rename = "raise-error")]
    pub raise_error: Option<TestCaseError>,
}

pub fn get_default_telemetry_topic<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_receiver) = default_prologue.receiver.as_ref() {
                if let Some(default_telemetry_topic) = default_receiver.telemetry_topic.as_ref() {
                    return Some(default_telemetry_topic.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_topic_namespace<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_receiver) = default_prologue.receiver.as_ref() {
                if let Some(default_topic_namespace) = default_receiver.topic_namespace.as_ref() {
                    return Some(default_topic_namespace.to_string());
                }
            }
        }
    }

    None
}
