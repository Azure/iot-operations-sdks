/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
pub enum AssetManagementGroupActionTypeSchema {
    #[serde(rename = "Call")]
    Call,
    #[serde(rename = "Read")]
    Read,
    #[serde(rename = "Write")]
    Write,
}
