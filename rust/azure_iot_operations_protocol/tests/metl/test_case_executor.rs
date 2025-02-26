// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::marker::PhantomData;

use serde::Deserialize;

use crate::metl::defaults::DefaultsType;
use crate::metl::test_case_duration::TestCaseDuration;
use crate::metl::test_case_error::TestCaseError;
use crate::metl::test_case_serializer::TestCaseSerializer;
use crate::metl::test_case_sync::TestCaseSync;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCaseExecutor<T: DefaultsType + Default> {
    #[serde(default)]
    pub defaults_type: PhantomData<T>,

    #[serde(rename = "command-name")]
    #[serde(default = "get_default_command_name::<T>")]
    pub command_name: Option<String>,

    #[serde(rename = "serializer")]
    #[serde(default = "TestCaseSerializer::get_default")]
    pub serializer: TestCaseSerializer<T>,

    #[serde(rename = "request-topic")]
    #[serde(default = "get_default_request_topic::<T>")]
    pub request_topic: Option<String>,

    #[serde(rename = "executor-id")]
    #[serde(default = "get_default_executor_id::<T>")]
    pub executor_id: Option<String>,

    #[serde(rename = "topic-namespace")]
    #[serde(default = "get_default_topic_namespace::<T>")]
    pub topic_namespace: Option<String>,

    #[serde(rename = "topic-token-map")]
    pub topic_token_map: Option<HashMap<String, String>>,

    #[serde(rename = "idempotent")]
    #[serde(default = "get_default_idempotent::<T>")]
    pub idempotent: bool,

    #[serde(rename = "cache-ttl")]
    #[serde(default = "get_default_cache_ttl::<T>")]
    pub cache_ttl: Option<TestCaseDuration>,

    #[serde(rename = "execution-timeout")]
    #[serde(default = "get_default_execution_timeout::<T>")]
    pub execution_timeout: Option<TestCaseDuration>,

    #[serde(rename = "request-responses-map")]
    #[serde(default = "get_default_request_responses_map::<T>")]
    pub request_responses_map: HashMap<String, Vec<String>>,

    #[serde(rename = "response-metadata")]
    #[serde(default)]
    pub response_metadata: HashMap<String, Option<String>>,

    #[serde(rename = "token-metadata-prefix")]
    pub token_metadata_prefix: Option<String>,

    #[serde(rename = "execution-concurrency")]
    #[serde(default = "get_default_execution_concurrency::<T>")]
    pub execution_concurrency: Option<i32>,

    #[serde(rename = "raise-error")]
    pub raise_error: Option<TestCaseError>,

    #[serde(rename = "sync")]
    #[serde(default)]
    pub sync: Vec<TestCaseSync>,
}

pub fn get_default_command_name<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_command_name) = default_executor.command_name.as_ref() {
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
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_request_topic) = default_executor.request_topic.as_ref() {
                    return Some(default_request_topic.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_executor_id<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_executor_id) = default_executor.executor_id.as_ref() {
                    return Some(default_executor_id.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_topic_namespace<T: DefaultsType + Default>() -> Option<String> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_topic_namespace) = default_executor.topic_namespace.as_ref() {
                    return Some(default_topic_namespace.to_string());
                }
            }
        }
    }

    None
}

pub fn get_default_idempotent<T: DefaultsType + Default>() -> bool {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_idempotent) = default_executor.idempotent {
                    return default_idempotent;
                }
            }
        }
    }

    false
}

pub fn get_default_cache_ttl<T: DefaultsType + Default>() -> Option<TestCaseDuration> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_cache_ttl) = default_executor.cache_ttl.as_ref() {
                    return Some((*default_cache_ttl).clone());
                }
            }
        }
    }

    None
}

pub fn get_default_execution_timeout<T: DefaultsType + Default>() -> Option<TestCaseDuration> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_execution_timeout) = default_executor.execution_timeout.as_ref()
                {
                    return Some((*default_execution_timeout).clone());
                }
            }
        }
    }

    None
}

pub fn get_default_request_responses_map<T: DefaultsType + Default>() -> HashMap<String, Vec<String>>
{
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_request_responses_map) =
                    default_executor.request_responses_map.as_ref()
                {
                    return (*default_request_responses_map).clone();
                }
            }
        }
    }

    HashMap::new()
}

pub fn get_default_execution_concurrency<T: DefaultsType + Default>() -> Option<i32> {
    if let Some(default_test_case) = T::get_defaults() {
        if let Some(default_prologue) = default_test_case.prologue.as_ref() {
            if let Some(default_executor) = default_prologue.executor.as_ref() {
                if let Some(default_execution_concurrency) = default_executor.execution_concurrency
                {
                    return Some(default_execution_concurrency);
                }
            }
        }
    }

    None
}
