/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

use serde::{Deserialize, Serialize};

/// Supported schema types
#[derive(Serialize, Deserialize, Debug, Clone)]
pub enum SchemaType {
    #[serde(rename = "MessageSchema")]
    MessageSchema,
}
