/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.9.0.0; DO NOT EDIT. */

use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
pub enum Format {
    #[serde(rename = "Delta/1.0")]
    Delta1,
    #[serde(rename = "JsonSchema/draft-07")]
    JsonSchemaDraft07,
}
