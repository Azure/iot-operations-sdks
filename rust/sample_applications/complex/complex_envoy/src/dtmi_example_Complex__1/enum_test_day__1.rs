/* Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT. */
#![allow(non_camel_case_types)]

use serde_repr::{Deserialize_repr, Serialize_repr};

/// An enumerated value for expressing a time.
#[derive(Serialize_repr, Deserialize_repr, Debug, Clone)]
#[repr(i32)]
pub enum Enum_Test_Day__1 {
    Today = 1,
    Tomorrow = 2,
}
