/* This file will be copied into the folder for generated code. */

use std::collections::HashMap;

use derive_builder::Builder;

#[allow(unused)]
#[derive(Builder, Clone)]
pub struct CommandExecutorOptions {
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    pub topic_namespace: Option<String>,
    /// Topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    pub topic_token_map: HashMap<String, String>,
}

#[allow(unused)]
#[derive(Builder, Clone)]
pub struct CommandInvokerOptions {
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    pub topic_namespace: Option<String>,
    /// Topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    pub topic_token_map: HashMap<String, String>,
    /// Prefix for the response topic.
    /// If all response topic options are `None`, the response topic will be generated
    /// based on the request topic in the form: `clients/<client_id>/<request_topic>`
    #[builder(default = "None")]
    pub response_topic_prefix: Option<String>,
    /// Suffix for the response topic.
    /// If all response topic options are `None`, the response topic will be generated
    /// based on the request topic in the form: `clients/<client_id>/<request_topic>`
    #[builder(default = "None")]
    pub response_topic_suffix: Option<String>,
}

#[allow(unused)]
#[derive(Builder, Clone)]
pub struct TelemetryReceiverOptions {
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    pub topic_namespace: Option<String>,
    /// Topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    pub topic_token_map: HashMap<String, String>,
    /// If true, telemetry messages are auto-acknowledged when received
    #[builder(default = "true")]
    pub auto_ack: bool,
}

#[allow(unused)]
#[derive(Builder, Clone)]
pub struct TelemetrySenderOptions {
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    pub topic_namespace: Option<String>,
    /// Topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    pub topic_token_map: HashMap<String, String>,
}
