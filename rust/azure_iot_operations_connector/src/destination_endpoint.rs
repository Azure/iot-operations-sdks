// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Destination Endpoints.

use std::{
    sync::{Arc, RwLock},
    time::Duration,
};

use azure_iot_operations_mqtt::{control_packet::QoS, session::SessionManagedClient};
use azure_iot_operations_protocol::{
    common::payload_serialize::{FormatIndicator, SerializedPayload},
    telemetry,
};
use azure_iot_operations_services::{
    azure_device_registry::{self, Dataset, MessageSchemaReference},
    state_store,
};

use crate::{Data, base_connector::ConnectorContext};

#[derive(Debug)]
pub(crate) struct Forwarder {
    message_schema_uri: Arc<RwLock<Option<MessageSchemaReference>>>,
    destination: Destination,
    connector_context: Arc<ConnectorContext>,
}
impl Forwarder {
    #[must_use]
    pub fn new_dataset_forwarder(
        dataset_definition: &Dataset,
        default_destination: Option<&Destination>,
        connector_context: Arc<ConnectorContext>,
    ) -> Self {
        // Create a new forwarder

        // If no destination is specified in the dataset definition, use the default dataset destination
        let destination = if dataset_definition.destinations.is_empty() {
            default_destination.expect(
                "Asset must have default dataset destination if dataset doesn't have destination",
            ).clone()
        } else {
            // for now, this vec will only ever be length 1
            let definition_destination = &dataset_definition.destinations;
            Destination::new_dataset_destination(definition_destination, &connector_context)
                .expect("Presence of destination already validated")
        };

        Self {
            message_schema_uri: Arc::new(RwLock::new(None)),
            destination,
            connector_context,
        }
    }

    /// # Errors
    /// TODO
    pub async fn send_data(&self, data: Data) -> Result<(), String> {
        // Forward the data to the destination
        match &self.destination {
            Destination::BrokerStateStore { key } => {
                match self
                    .connector_context
                    .state_store_client
                    .set(
                        key.clone().into(),
                        data.payload,
                        self.connector_context.default_timeout,
                        None,
                        state_store::SetOptions {
                            expires: None, // TODO: expiry?
                            ..state_store::SetOptions::default()
                        },
                    )
                    .await
                {
                    Ok(res) => {
                        if res.response {
                            Ok(())
                        } else {
                            // This shouldn't be possible since SetOptions are unconditional
                            Err("Failed to set value".to_string())
                        }
                    }
                    Err(e) => {
                        // TODO: translate this to a meaningful error type
                        Err(e.to_string())
                    }
                }
            }
            Destination::Mqtt {
                qos,
                retain: _,
                ttl,
                telemetry_sender,
            } => {
                // create MQTT message, setting schema id to response from SR (message_schema_uri)
                // TODO: cloud event
                // TODO: retain
                let cloud_event = telemetry::sender::CloudEventBuilder::default()
                    .source("something")
                    // .time(data.timestamp)
                    // .data_schema("from message schema reference")
                    .build()
                    .unwrap();
                let message = telemetry::sender::MessageBuilder::default()
                    .payload(SerializedPayload {
                        content_type: data.content_type.unwrap_or_default(),
                        payload: data.payload,
                        format_indicator: FormatIndicator::default(),
                    })
                    .unwrap() // TODO: need a way to return this back to the read_telemetry func probably
                    .message_expiry(Duration::from_secs(*ttl)) // TODO: value?
                    .qos(*qos)
                    // .custom_user_data(vec![(
                    //     "schemaId".to_string(),
                    //     schema_info.clone().unwrap_or_default(),
                    // )])
                    .cloud_event(cloud_event)
                    .build()
                    .unwrap();
                // send message with telemetry::Sender
                match telemetry_sender.send(message).await {
                    Ok(()) => Ok(()),
                    Err(e) => {
                        // TODO: translate this to a meaningful error type
                        Err(e.to_string())
                    }
                }
            }
            Destination::Storage { path: _ } => Ok(()),
        }
        // Ok(())
    }

    /// Sets the message schema uri for this forwarder to use
    ///
    /// # Panics
    /// if the message schema uri mutex has been poisoned, which should not be possible
    pub fn update_message_schema_uri(&self, message_schema_uri: Option<MessageSchemaReference>) {
        // Add the message schema URI to the forwarder
        *self.message_schema_uri.write().unwrap() = message_schema_uri;
    }
}

#[derive(Clone)]
pub(crate) enum Destination {
    BrokerStateStore {
        key: String,
    },
    Mqtt {
        // topic: String,
        qos: QoS,
        retain: bool,
        ttl: u64,
        telemetry_sender: telemetry::Sender<SerializedPayload, SessionManagedClient>,
    },
    Storage {
        path: String,
    },
}

impl Destination {
    #[must_use]
    pub(crate) fn new_dataset_destination(
        dataset_destinations: &[azure_device_registry::DatasetDestination],
        connector_context: &Arc<ConnectorContext>,
    ) -> Option<Self> {
        // Create a new forwarder
        if dataset_destinations.is_empty() {
            None
        } else {
            // for now, this vec will only ever be length 1
            let definition_destination = &dataset_destinations[0];
            let destination = match definition_destination.target {
                azure_device_registry::DatasetTarget::BrokerStateStore => {
                    Destination::BrokerStateStore {
                        key: definition_destination
                            .configuration
                            .key
                            .clone()
                            .expect("Key must be present if Target is BrokerStateStore"),
                    }
                }
                azure_device_registry::DatasetTarget::Mqtt => {
                    let telemetry_sender_options = telemetry::sender::OptionsBuilder::default()
                        .topic_pattern(
                            definition_destination
                                .configuration
                                .topic
                                .clone()
                                .expect("Topic must be present if Target is Mqtt"),
                        )
                        .build()
                        .unwrap();
                    let telemetry_sender = telemetry::Sender::new(
                        connector_context.application_context.clone(),
                        connector_context.managed_client.clone(),
                        telemetry_sender_options,
                    )
                    .unwrap();
                    Destination::Mqtt {
                        // topic: definition_destination.configuration.topic.clone().expect("Topic must be present if Target is Mqtt"),
                        qos: definition_destination
                            .configuration
                            .qos
                            .expect("QoS must be present if Target is Mqtt"),
                        retain: matches!(
                            definition_destination
                                .configuration
                                .retain
                                .clone()
                                .expect("Retain must be present if Target is Mqtt"),
                            azure_device_registry::Retain::Keep
                        ),
                        ttl: definition_destination
                            .configuration
                            .ttl
                            .expect("TTL must be present if Target is Mqtt"),
                        telemetry_sender,
                    }
                }
                azure_device_registry::DatasetTarget::Storage => Destination::Storage {
                    path: definition_destination
                        .configuration
                        .path
                        .clone()
                        .expect("Path must be present if Target is Storage"),
                },
            };
            Some(destination)
        }
    }
}

impl std::fmt::Debug for Destination {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            Self::BrokerStateStore { key } => f
                .debug_struct("BrokerStateStore")
                .field("key", key)
                .finish(),
            Self::Mqtt {
                qos,
                retain,
                ttl,
                telemetry_sender: _,
            } => f
                .debug_struct("Mqtt")
                .field("qos", qos)
                .field("retain", retain)
                .field("ttl", ttl)
                // .field("telemetry_sender", telemetry_sender)
                .finish(),
            Self::Storage { path } => f.debug_struct("Storage").field("path", path).finish(),
        }
    }
}
