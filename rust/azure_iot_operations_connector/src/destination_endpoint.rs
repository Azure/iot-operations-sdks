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
    azure_device_registry::{self, MessageSchemaReference},
    state_store,
};
use chrono::{DateTime, Utc};
use thiserror::Error;

use crate::{AdrConfigError, Data, base_connector::ConnectorContext};

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

// TODO: Once we have retriable/not retriable designators on underlying errors, this should
// split into StateError (Missing Message Schema), RetriableError(Network errors), and
// NonRetriableError (Invalid data, etc)
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
    ValidationError(String),
}

/// A [`Forwarder`] forwards [`Data`] to a destination defined in a dataset or asset
#[derive(Debug)]
pub(crate) struct Forwarder {
    message_schema_reference: Arc<RwLock<Option<MessageSchemaReference>>>,
    destination: ForwarderDestination,
    connector_context: Arc<ConnectorContext>,
}
impl Forwarder {
    /// Creates a new [`Forwarder`] from a dataset definition's Destinations
    /// and default destinations, if present on the asset
    ///
    /// # Errors
    /// [`AdrConfigError`] if there are any issues processing
    /// the destination from the definitions. This can be used to report the error
    /// to the ADR service on the dataset's status
    pub(crate) fn new_dataset_forwarder(
        dataset_destinations: &[azure_device_registry::DatasetDestination],
        inbound_endpoint_name: &str,
        default_destinations: &[Arc<Destination>],
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        // if the dataset has destinations defined, use them, otherwise use the default dataset destinations
        let destination = match Destination::new_dataset_destinations(
            dataset_destinations,
            inbound_endpoint_name,
            &connector_context,
        )?
        // for now, this vec will only ever be length 1
        .pop()
        {
            Some(destination) => ForwarderDestination::DatasetDestination(destination),
            None => {
                if default_destinations.is_empty() {
                    Err(AdrConfigError {
                                code: None,
                                details: None,
                                inner_error: None,
                                message: Some("Asset must have default dataset destinations if dataset doesn't have destinations".to_string()),
                            })?
                } else {
                    // for now, this vec will only ever be length 1
                    ForwarderDestination::DefaultDestination(default_destinations[0].clone())
                }
            }
        };

        Ok(Self {
            message_schema_reference: Arc::new(RwLock::new(None)),
            destination,
            connector_context,
        })
    }

    /// Forwards [`Data`] to the destination
    /// Returns once the message has been sent successfully
    ///
    /// # Errors
    /// [`struct@Error`] of kind [`MissingMessageSchema`](ErrorKind::MissingMessageSchema)
    /// if the [`MessageSchema`] has not been reported yet. This is required before forwarding any data
    ///
    /// [`struct@Error`] of kind [`DataValidationError`](ErrorKind::MqttTelemetryError)
    /// if the [`Data`] isn't valid.
    ///
    /// [`struct@Error`] of kind [`BrokerStateStoreError`](ErrorKind::BrokerStateStoreError)
    /// if the destination is `BrokerStateStore` and there are any errors setting the data with the service
    ///
    /// [`struct@Error`] of kind [`MqttTelemetryError`](ErrorKind::MqttTelemetryError)
    /// if the destination is `Mqtt` and there are any errors sending the message to the broker
    ///
    /// # Panics
    /// if the message schema reference mutex has been poisoned, which should not be possible
    pub(crate) async fn send_data(&self, data: Data) -> Result<(), Error> {
        // Forward the data to the destination
        let destination = match &self.destination {
            ForwarderDestination::DefaultDestination(destination) => destination.as_ref(),
            ForwarderDestination::DatasetDestination(destination) => destination,
        };
        match destination {
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
                retain,
                ttl,
                inbound_endpoint_name,
                telemetry_sender,
            } => {
                // create MQTT message, setting schema id to response from SR (message_schema_uri)
                // TODO: cloud event
                // TODO: remove once message schema validation is turned back on
                #[allow(clippy::manual_map)]
                let message_schema_uri = if let Some(message_schema_reference) =
                    self.message_schema_reference.read().unwrap().as_ref()
                {
                    Some(format!(
                        "aio-sr://{}/{}:{}",
                        message_schema_reference.registry_namespace,
                        message_schema_reference.name,
                        message_schema_reference.version
                    ))
                } else {
                    // TODO: validate this for other destinations as well
                    // Commented out to remove the requirement for message schema temporarily
                    // return Err(Error(ErrorKind::MissingMessageSchema));
                    None
                };
                let mut cloud_event_builder = telemetry::sender::CloudEventBuilder::default();
                cloud_event_builder.source(inbound_endpoint_name);
                // .event_type("something?")
                if let Some(message_schema_uri) = message_schema_uri {
                    cloud_event_builder.data_schema(message_schema_uri);
                }
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
                        // source that isn't a uri reference
                        // data_schema that isn't a valid uri - don't think this is possible since we create it
                        telemetry::sender::CloudEventBuilderError::ValidationError(e) => {
                            Error(ErrorKind::ValidationError(e))
                        }
                        e => Error(ErrorKind::ValidationError(e.to_string())),
                    }
                })?;
                let mut message_builder = telemetry::sender::MessageBuilder::default();
                if let Some(qos) = qos {
                    message_builder.qos(*qos);
                }
                if let Some(ttl) = ttl {
                    message_builder.message_expiry(Duration::from_secs(*ttl));
                }
                if let Some(retain) = retain {
                    message_builder.retain(*retain);
                }
                // Can return an error if content type isn't valid UTF-8. Serialization can't fail
                message_builder
                    .payload(BypassPayload {
                        content_type: data.content_type,
                        payload: data.payload,
                        format_indicator: FormatIndicator::default(),
                    })
                    .map_err(|e| ErrorKind::ValidationError(e.to_string()))?;
                message_builder.cloud_event(cloud_event);
                message_builder.custom_user_data(data.custom_user_data);
                // This validates the content type and custom user data
                let message = message_builder
                    .build()
                    .map_err(|e| ErrorKind::ValidationError(e.to_string()))?;
                // send message with telemetry::Sender
                Ok(telemetry_sender
                    .send(message)
                    .await
                    .map_err(ErrorKind::from)?)
            }
            Destination::Storage { path: _ } => {
                // TODO: implement
                log::error!("Storage destination not implemented");
                unimplemented!()
            }
        }
    }

    /// Sets the message schema reference for this forwarder to use. Must be done before
    /// calling `send_data`
    ///
    /// # Panics
    /// if the message schema reference mutex has been poisoned, which should not be possible
    pub(crate) fn update_message_schema_reference(
        &self,
        message_schema_reference: Option<MessageSchemaReference>,
    ) {
        // Add the message schema URI to the forwarder
        *self.message_schema_reference.write().unwrap() = message_schema_reference;
    }
}

#[derive(Debug)]
pub(crate) enum ForwarderDestination {
    DefaultDestination(Arc<Destination>),
    DatasetDestination(Destination),
}

#[allow(dead_code)]
pub(crate) enum Destination {
    BrokerStateStore {
        key: String,
    },
    Mqtt {
        qos: Option<QoS>, // these are optional so that we use the defaults from the telemetry::sender if they aren't specified on the dataset/asset definition
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
    /// Creates a list of new [`Destination`]s from a list of [`azure_device_registry::DatasetDestination`]s.
    /// At this time, this list cannot have more than one element. If there are no items in the list,
    /// this function will return an empty Vec. This isn't an error, since a default destination may or
    /// may not exist in the definition.
    ///
    /// # Errors
    /// [`AdrConfigError`] if the destination is `Mqtt` and the topic is invalid.
    /// This can be used to report the error to the ADR service on the status
    pub(crate) fn new_dataset_destinations(
        dataset_destinations: &[azure_device_registry::DatasetDestination],
        inbound_endpoint_name: &str,
        connector_context: &Arc<ConnectorContext>,
    ) -> Result<Vec<Self>, AdrConfigError> {
        // Create a new forwarder
        if dataset_destinations.is_empty() {
            Ok(vec![])
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
                        .map_err(|e| AdrConfigError {
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
                    .map_err(|e| AdrConfigError {
                        code: None,
                        details: None,
                        inner_error: None,
                        message: Some(e.to_string()),
                    })?;
                    Destination::Mqtt {
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
                azure_device_registry::DatasetTarget::Storage => {
                    Err(AdrConfigError {
                        code: None,
                        details: None,
                        inner_error: None,
                        message: Some(
                            "Storage destination not supported for this connector".to_string(),
                        ),
                    })?
                    // Destination::Storage {
                    //     path: definition_destination
                    //         .configuration
                    //         .path
                    //         .clone()
                    //         .expect("Path must be present if Target is Storage"),
                    // }
                }
            };
            Ok(vec![destination])
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
