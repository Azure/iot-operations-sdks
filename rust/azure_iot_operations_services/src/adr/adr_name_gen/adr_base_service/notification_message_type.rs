/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
pub enum NotificationMessageType {
    #[serde(rename = "Off")]
    Off,
    #[serde(rename = "On")]
    On,
}
