use serde::Deserialize;

use crate::metl::test_case_duration::TestCaseDuration;

#[derive(Deserialize, Debug)]
pub struct DefaultInvokeCommand {
    #[serde(rename = "command-name")]
    pub command_name: Option<String>,

    #[serde(rename = "executor-id")]
    pub executor_id: Option<String>,

    #[serde(rename = "request-value")]
    pub request_value: Option<String>,

    #[serde(rename = "timeout")]
    pub timeout: Option<TestCaseDuration>,
}
