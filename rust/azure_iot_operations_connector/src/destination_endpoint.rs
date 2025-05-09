// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Destination Endpoints.

#![allow(missing_docs)]

use std::sync::{Arc, RwLock};

use azure_iot_operations_services::azure_device_registry::{Dataset, MessageSchemaReference};

use crate::Data;

pub struct Forwarder {
    message_schema_uri: Arc<RwLock<Option<MessageSchemaReference>>>,
}
impl Forwarder {
    #[must_use]
    pub fn new(_dataset_definition: Dataset) -> Self {
        // Create a new forwarder
        Self {
            message_schema_uri: Arc::new(RwLock::new(None)),
        }
    }

    /// # Errors
    /// TODO
    #[allow(clippy::unused_async)]
    pub async fn send_data(&self, _data: Data) -> Result<(), String> {
        // Forward the data to the destination
        Err("Not implemented".to_string())
    }
    pub fn update_message_schema_uri(&self, message_schema_uri: Option<MessageSchemaReference>) {
        // Add the message schema URI to the forwarder
        *self.message_schema_uri.write().unwrap() = message_schema_uri;
    }
}
