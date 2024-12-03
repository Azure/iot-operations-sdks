// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use serde::Deserialize;
use std::fs;
use std::sync::OnceLock;

use crate::metl::default_test_case::DefaultTestCase;

static INVOKER_DEFAULTS: OnceLock<DefaultTestCase> = OnceLock::new();
static EXECUTOR_DEFAULTS: OnceLock<DefaultTestCase> = OnceLock::new();

pub fn get_invoker_defaults() -> Option<&'static DefaultTestCase> {
    return INVOKER_DEFAULTS.get();
}

pub fn get_executor_defaults() -> Option<&'static DefaultTestCase> {
    return EXECUTOR_DEFAULTS.get();
}

#[ctor::ctor]
fn init() {
    INVOKER_DEFAULTS
        .set(get_default_test_case("CommandInvoker"))
        .unwrap();
    EXECUTOR_DEFAULTS
        .set(get_default_test_case("CommandExecutor"))
        .unwrap();
}

fn get_default_test_case(folder_name: &str) -> DefaultTestCase {
    let file_path = format!("../../eng/test/test-cases/Protocol/{folder_name}/defaults.toml");
    let toml_text = fs::read_to_string(file_path).unwrap();
    return toml::from_str(toml_text.as_str()).unwrap();
}

pub trait DefaultsType {
    fn get_defaults() -> Option<&'static DefaultTestCase>;
}

#[derive(Clone, Deserialize, Default, Debug)]
pub struct InvokerDefaults {}

#[derive(Clone, Deserialize, Default, Debug)]
pub struct ExecutorDefaults {}

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
