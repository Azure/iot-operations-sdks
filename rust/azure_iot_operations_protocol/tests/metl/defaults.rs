// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;
use std::fs;
use std::sync::OnceLock;

use crate::metl::default_test_case::DefaultTestCase;

static INVOKER_DEFAULTS: OnceLock<DefaultTestCase> = OnceLock::new();
static EXECUTOR_DEFAULTS: OnceLock<DefaultTestCase> = OnceLock::new();
static RECEIVER_DEFAULTS: OnceLock<DefaultTestCase> = OnceLock::new();
static SENDER_DEFAULTS: OnceLock<DefaultTestCase> = OnceLock::new();

pub fn get_invoker_defaults() -> Option<&'static DefaultTestCase> {
    INVOKER_DEFAULTS.get()
}

pub fn get_executor_defaults() -> Option<&'static DefaultTestCase> {
    EXECUTOR_DEFAULTS.get()
}

pub fn get_receiver_defaults() -> Option<&'static DefaultTestCase> {
    RECEIVER_DEFAULTS.get()
}

pub fn get_sender_defaults() -> Option<&'static DefaultTestCase> {
    SENDER_DEFAULTS.get()
}

#[ctor::ctor]
fn init() {
    INVOKER_DEFAULTS
        .set(get_default_test_case("CommandInvoker"))
        .unwrap();
    EXECUTOR_DEFAULTS
        .set(get_default_test_case("CommandExecutor"))
        .unwrap();
    RECEIVER_DEFAULTS
        .set(get_default_test_case("TelemetryReceiver"))
        .unwrap();
    SENDER_DEFAULTS
        .set(get_default_test_case("TelemetrySender"))
        .unwrap();
}

fn get_default_test_case(folder_name: &str) -> DefaultTestCase {
    let file_path = format!("../../eng/test/test-cases/Protocol/{folder_name}/defaults.toml");
    let toml_text = fs::read_to_string(file_path).unwrap();
    toml::from_str(toml_text.as_str()).unwrap()
}

pub trait DefaultsType {
    fn get_defaults() -> Option<&'static DefaultTestCase>;
}

#[derive(Clone, Deserialize, Default, Debug)]
pub struct InvokerDefaults {}

#[derive(Clone, Deserialize, Default, Debug)]
pub struct ExecutorDefaults {}

#[derive(Clone, Deserialize, Default, Debug)]
pub struct ReceiverDefaults {}

#[derive(Clone, Deserialize, Default, Debug)]
pub struct SenderDefaults {}

impl DefaultsType for InvokerDefaults {
    fn get_defaults() -> Option<&'static DefaultTestCase> {
        get_invoker_defaults()
    }
}

impl DefaultsType for ExecutorDefaults {
    fn get_defaults() -> Option<&'static DefaultTestCase> {
        get_executor_defaults()
    }
}

impl DefaultsType for ReceiverDefaults {
    fn get_defaults() -> Option<&'static DefaultTestCase> {
        get_receiver_defaults()
    }
}

impl DefaultsType for SenderDefaults {
    fn get_defaults() -> Option<&'static DefaultTestCase> {
        get_sender_defaults()
    }
}
