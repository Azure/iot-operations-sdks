/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
pub enum QoS {
    #[serde(rename = "Qos0")]
    Qos0,
    #[serde(rename = "Qos1")]
    Qos1,
}
