/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT. */

use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone)]
pub enum SupportedAuthenticationMethodsSchemaElementSchema {
    #[serde(rename = "Anonymous")]
    Anonymous,
    #[serde(rename = "Certificate")]
    Certificate,
    #[serde(rename = "UsernamePassword")]
    UsernamePassword,
}
