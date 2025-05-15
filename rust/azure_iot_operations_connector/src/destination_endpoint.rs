// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Destination Endpoints.

use std::{
    sync::{Arc, RwLock},
    time::Duration,
};

use azure_iot_operations_mqtt::{control_packet::QoS, session::SessionManagedClient};
use azure_iot_operations_protocol::{
    common::{
        aio_protocol_error::AIOProtocolError,
        payload_serialize::{BypassPayload, FormatIndicator},
    },
    telemetry,
};
use azure_iot_operations_services::{
    azure_device_registry::{self, Dataset, MessageSchemaReference},
    state_store,
};
use chrono::{DateTime, Utc};
use thiserror::Error;

use crate::{Data, base_connector::ConnectorContext};

/// Represents an error that occurred in the [`Forwarder`].
#[derive(Debug, Error)]
#[error(transparent)]
pub struct Error(#[from] ErrorKind);

impl Error {
    /// Returns the [`ErrorKind`] of the error.
    #[must_use]
    pub fn kind(&self) -> &ErrorKind {
        &self.0
    }
}

/// Represents the kinds of errors that occur in the [`Forwarder`] implementation.
#[derive(Error, Debug)]
pub enum ErrorKind {
    /// Message Schema must be present before data can be forwarded
    #[error("Message Schema must be reported before data can be forwarded")]
    MissingMessageSchema,
    /// An error occurred while forwarding data to the State Store
    #[error(transparent)]
    BrokerStateStoreError(#[from] state_store::Error),
    /// An error occurred while forwarding data as MQTT Telemetry
    #[error(transparent)]
    MqttTelemetryError(#[from] AIOProtocolError),
    /// Data provided to be forwarded is invalid
    #[error("Error with contents of Data: {0}")]
    DataValidationError(String),
}

#[derive(Debug)]
pub(crate) struct Forwarder {
    message_schema_reference: Arc<RwLock<Option<MessageSchemaReference>>>,
    destination: Destination,
    connector_context: Arc<ConnectorContext>,
}
impl Forwarder {
    pub fn new_dataset_forwarder(
        dataset_definition: &Dataset,
        inbound_endpoint_name: &str,
        default_destination: Option<&Destination>,
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, azure_device_registry::ConfigError> {
        // Create a new forwarder

        // If no destination is specified in the dataset definition, use the default dataset destination
        let destination = if dataset_definition.destinations.is_empty() {
            default_destination.ok_or(azure_device_registry::ConfigError {
                code: None,
                details: None,
                inner_error: None,
                message: Some("Asset must have default dataset destination if dataset doesn't have destination".to_string()),
            })?.clone()
        } else {
            // for now, this vec will only ever be length 1
            let definition_destination = &dataset_definition.destinations;
            Destination::new_dataset_destination(
                definition_destination,
                inbound_endpoint_name,
                &connector_context,
            )?
            .expect("Presence of destination already validated")
        };

        Ok(Self {
            message_schema_reference: Arc::new(RwLock::new(None)),
            destination,
            connector_context,
        })
    }

    /// # Errors
    /// TODO
    /// # Panics
    /// if the message schema reference mutex has been poisoned, which should not be possible
    pub async fn send_data(&self, data: Data) -> Result<(), Error> {
        // Forward the data to the destination
        match &self.destination {
            Destination::BrokerStateStore { key } => {
                if self
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
                    .map_err(ErrorKind::from)?
                    .response
                {
                    Ok(())
                } else {
                    // This shouldn't be possible since SetOptions are unconditional
                    unreachable!()
                }
            }
            Destination::Mqtt {
                qos,
                retain: _,
                ttl,
                inbound_endpoint_name,
                telemetry_sender,
            } => {
                // create MQTT message, setting schema id to response from SR (message_schema_uri)
                // TODO: cloud event
                // TODO: retain
                let message_schema_uri = if let Some(message_schema_reference) =
                    self.message_schema_reference.read().unwrap().as_ref()
                {
                    format!(
                        "aio-sr://{}/{}:{}",
                        message_schema_reference.registry_namespace,
                        message_schema_reference.name,
                        message_schema_reference.version
                    )
                } else {
                    // TODO: validate this for other destinations as well
                    return Err(Error(ErrorKind::MissingMessageSchema));
                };
                let mut cloud_event_builder = telemetry::sender::CloudEventBuilder::default();
                cloud_event_builder
                    .source(inbound_endpoint_name)
                    // .event_type("something?")
                    .data_schema(message_schema_uri);
                if let Some(hlc) = data.timestamp {
                    cloud_event_builder.time(DateTime::<Utc>::from(hlc.timestamp));
                }
                let cloud_event = cloud_event_builder.build().map_err(|e| {
                    match e {
                        // since we specify `source`, all required fields will always be present
                        telemetry::sender::CloudEventBuilderError::UninitializedField(_) => {
                            unreachable!()
                        }
                        // This can be caused by a
                        // source that isn't a uri reference - check
                        // data_schema that isn't a valid uri - check, don't think this is possible since we create it
                        telemetry::sender::CloudEventBuilderError::ValidationError(e) => {
                            Error(ErrorKind::DataValidationError(e))
                        }
                        e => Error(ErrorKind::DataValidationError(e.to_string())),
                    }
                })?;
                let mut message_builder = telemetry::sender::MessageBuilder::default();
                if let Some(qos) = qos {
                    message_builder.qos(*qos);
                }
                if let Some(ttl) = ttl {
                    message_builder.message_expiry(Duration::from_secs(*ttl));
                }
                // Can return an error if content type isn't valid UTF-8. Serialization can't fail
                message_builder
                    .payload(BypassPayload {
                        content_type: data.content_type,
                        payload: data.payload,
                        format_indicator: FormatIndicator::default(),
                    })
                    .map_err(|e| ErrorKind::DataValidationError(e.to_string()))?;
                message_builder.cloud_event(cloud_event);
                message_builder.custom_user_data(data.custom_user_data);
                // This validates the content type and custom user data
                let message = message_builder
                    .build()
                    .map_err(|e| ErrorKind::DataValidationError(e.to_string()))?;
                // send message with telemetry::Sender
                Ok(telemetry_sender
                    .send(message)
                    .await
                    .map_err(ErrorKind::from)?)
            }
            Destination::Storage { path: _ } => Ok(()), // TODO: implement
        }
    }

    /// Sets the message schema reference for this forwarder to use
    ///
    /// # Panics
    /// if the message schema reference mutex has been poisoned, which should not be possible
    pub fn update_message_schema_reference(
        &self,
        message_schema_reference: Option<MessageSchemaReference>,
    ) {
        // Add the message schema URI to the forwarder
        *self.message_schema_reference.write().unwrap() = message_schema_reference;
    }
}

#[derive(Clone)]
pub(crate) enum Destination {
    BrokerStateStore {
        key: String,
    },
    Mqtt {
        // topic: String,
        qos: Option<QoS>,
        retain: Option<bool>,
        ttl: Option<u64>,
        inbound_endpoint_name: String,
        telemetry_sender: telemetry::Sender<BypassPayload, SessionManagedClient>,
    },
    Storage {
        path: String,
    },
}

impl Destination {
    pub(crate) fn new_dataset_destination(
        dataset_destinations: &[azure_device_registry::DatasetDestination],
        inbound_endpoint_name: &str,
        connector_context: &Arc<ConnectorContext>,
    ) -> Result<Option<Self>, azure_device_registry::ConfigError> {
        // Create a new forwarder
        if dataset_destinations.is_empty() {
            Ok(None)
        } else {
            // for now, this vec will only ever be length 1
            let definition_destination = &dataset_destinations[0];
            let destination = match definition_destination.target {
                azure_device_registry::DatasetTarget::BrokerStateStore => {
                    Destination::BrokerStateStore {
                        // TODO: validate key not empty?
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
                        // TODO: check if this can fail, or just the next one
                        .map_err(|e| azure_device_registry::ConfigError {
                            code: None,
                            details: None,
                            inner_error: None,
                            message: Some(e.to_string()),
                        })?; // can fail if topic isn't valid in config
                    let telemetry_sender = telemetry::Sender::new(
                        connector_context.application_context.clone(),
                        connector_context.managed_client.clone(),
                        telemetry_sender_options,
                    )
                    .map_err(|e| azure_device_registry::ConfigError {
                        code: None,
                        details: None,
                        inner_error: None,
                        message: Some(e.to_string()),
                    })?;
                    Destination::Mqtt {
                        // topic: definition_destination.configuration.topic.clone().expect("Topic must be present if Target is Mqtt"),
                        qos: definition_destination.configuration.qos,
                        retain: definition_destination
                            .configuration
                            .retain
                            .as_ref()
                            .map(|r| matches!(r, azure_device_registry::Retain::Keep)),
                        ttl: definition_destination.configuration.ttl,
                        inbound_endpoint_name: inbound_endpoint_name.to_string(),
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
            Ok(Some(destination))
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
                inbound_endpoint_name,
                telemetry_sender: _,
            } => f
                .debug_struct("Mqtt")
                .field("qos", qos)
                .field("retain", retain)
                .field("ttl", ttl)
                .field("inbound_endpoint_name", inbound_endpoint_name)
                // .field("telemetry_sender", telemetry_sender)
                .finish(),
            Self::Storage { path } => f.debug_struct("Storage").field("path", path).finish(),
        }
    }
}
