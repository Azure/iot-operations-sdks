// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::collections::HashMap;
use std::marker::PhantomData;

use serde::Deserialize;

use crate::metl::defaults::DefaultsType;
use crate::metl::test_case_catch::TestCaseCatch;
use crate::metl::test_case_executor::TestCaseExecutor;
use crate::metl::test_case_invoker::TestCaseInvoker;
use crate::metl::test_case_mqtt_config::TestCaseMqttConfig;
use crate::metl::test_case_push_acks::TestCasePushAcks;
use crate::metl::test_case_receiver::TestCaseReceiver;
use crate::metl::test_case_sender::TestCaseSender;

#[derive(Clone, Deserialize, Debug)]
#[allow(dead_code)]
pub struct TestCasePrologue<T: DefaultsType + Default> {
    #[serde(default)]
    pub defaults_type: PhantomData<T>,

    #[serde(rename = "mqtt-config")]
    #[serde(default = "TestCaseMqttConfig::get_default")]
    pub mqtt_config: TestCaseMqttConfig,

    #[serde(rename = "push-acks")]
    pub push_acks: Option<TestCasePushAcks>,

    #[serde(rename = "executors")]
    #[serde(default)]
    pub executors: Vec<TestCaseExecutor<T>>,

    #[serde(rename = "invokers")]
    #[serde(default)]
    pub invokers: Vec<TestCaseInvoker<T>>,

    #[serde(rename = "receivers")]
    #[serde(default)]
    pub receivers: Vec<TestCaseReceiver<T>>,

    #[serde(rename = "senders")]
    #[serde(default)]
    pub senders: Vec<TestCaseSender<T>>,

    #[serde(rename = "catch")]
    pub catch: Option<TestCaseCatch>,

    #[serde(rename = "countdown-events")]
    #[serde(default)]
    pub countdown_events: HashMap<String, i32>,
}
