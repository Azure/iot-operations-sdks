// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types for Azure Device Registry operations.

/// Azure Device Registry generated code
mod device_name_gen;

#[derive(Clone, Debug)]
/// Represents a reference to a schema, including its name, version, and namespace.
pub struct MessageSchemaReference {
    /// The name of the message schema.
    pub message_schema_name: String,
    /// The version of the message schema.
    pub message_schema_version: String,
    /// The namespace of the message schema.
    pub message_schema_namespace: String,
}

impl From<MessageSchemaReference> for adr_base_service_gen::MessageSchemaReference {
    fn from(value: MessageSchemaReference) -> Self {
        adr_base_service_gen::MessageSchemaReference {
            schema_name: value.message_schema_name,
            schema_version: value.message_schema_version,
            schema_registry_namespace: value.message_schema_namespace,
        }
    }
}
