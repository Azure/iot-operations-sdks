/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

use std::collections::HashMap;

use derive_builder::Builder;

#[allow(unused)]
#[derive(Builder, Clone)]
pub struct CommandOptions {
    /// Optional Topic namespace to be prepended to the topic pattern
    #[builder(default = "None")]
    pub topic_namespace: Option<String>,
    /// Topic token keys/values to be replaced in the topic pattern
    #[builder(default)]
    pub topic_token_map: HashMap<String, String>,
}

#[allow(unused)]
#[derive(Builder, Clone)]
pub struct TelemetryOptions {
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
