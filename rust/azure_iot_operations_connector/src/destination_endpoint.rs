// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Traits, types, and implementations for Azure IoT Operations Connector Destination Endpoints.

use std::{sync::Arc, time::Duration};

use azure_iot_operations_mqtt::{aio::cloud_event as aio_cloud_event, control_packet::QoS};
use azure_iot_operations_protocol::{
    common::{
        CloudEventSubject,
        aio_protocol_error::AIOProtocolError,
        hybrid_logical_clock::HybridLogicalClock,
        payload_serialize::{BypassPayload, FormatIndicator},
    },
    telemetry,
};
use azure_iot_operations_services::{azure_device_registry::models as adr_models, state_store};
use chrono::{DateTime, Utc};
use thiserror::Error;

use crate::{
    AdrConfigError, Data, DataOperationName, DataOperationRef, base_connector::ConnectorContext,
    deployment_artifacts::azure_device_registry::AssetRef,
};

/// Represents an error that occurred when forwarding data.
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
/// Represents the kinds of errors that occur when forwarding data.
#[derive(Error, Debug)]
#[non_exhaustive]
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
    /// Data provided to be forwarded is invalid or there is no valid destination
    #[error("Error with Destination or contents of Data: {0}")]
    ValidationError(String),
}

/// Represents whether there is currently a valid Forwarder or not for a Data Operation
#[derive(Debug)]
pub(crate) enum DataOperationForwarder {
    Forwarder(Box<Forwarder>),
    Error(AdrConfigError),
}

impl DataOperationForwarder {
    /// Wrapper to forward [`Data`] to the destination if a valid forwarder exists
    pub(crate) async fn send_data(
        &self,
        data: Data,
        protocol_specific_identifier: Option<&str>,
    ) -> Result<(), Error> {
        match self {
            DataOperationForwarder::Forwarder(forwarder) => {
                forwarder
                    .send_data(data, protocol_specific_identifier)
                    .await
            }
            DataOperationForwarder::Error(_) => Err(ErrorKind::ValidationError(
                "No valid destination configured for data operation".to_string(),
            )
            .into()),
        }
    }
}

/// A [`Forwarder`] forwards [`Data`] to a destination defined in a data operation or asset
#[derive(Debug)]
pub(crate) struct Forwarder {
    message_schema_reference: Option<adr_models::MessageSchemaReference>,
    destination: ForwarderDestination,
    device_uuid: Option<String>,
    device_external_device_id: Option<String>,
    data_source: Option<String>,
    data_operation_name: DataOperationName,
    data_operation_type_ref: Option<String>,
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
    #[allow(clippy::too_many_arguments)]
    pub(crate) fn new_dataset_forwarder(
        dataset: &adr_models::Dataset,
        default_destinations: &[Arc<Destination>],
        asset_ref: &AssetRef,
        device_uuid: Option<String>,
        device_external_device_id: Option<String>,
        asset_uuid: Option<&String>,
        asset_external_asset_id: Option<&String>,
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        // Use internal new fn with dataset destinations
        Self::new_data_operation_forwarder(
            Destination::new_dataset_destinations(
                &dataset.destinations,
                asset_ref,
                asset_uuid,
                asset_external_asset_id,
                &connector_context,
            )?,
            default_destinations,
            device_uuid,
            device_external_device_id,
            dataset.data_source.clone(),
            DataOperationName::Dataset {
                name: dataset.name.clone(),
            },
            dataset.type_ref.clone(),
            connector_context,
        )
    }

    /// Creates a new [`Forwarder`] from an event/stream definition's Destinations
    /// and default destinations, if present on the asset
    ///
    /// # Errors
    /// [`AdrConfigError`] if there are any issues processing
    /// the destination from the definitions. This can be used to report the error
    /// to the ADR service on the event/stream's status
    #[allow(clippy::too_many_arguments)]
    pub(crate) fn new_event_stream_forwarder(
        event_stream_destinations: &[adr_models::EventStreamDestination],
        default_destinations: &[Arc<Destination>],
        data_operation_ref: &DataOperationRef,
        data_source: Option<String>,
        data_operation_type_ref: Option<String>,
        device_uuid: Option<String>,
        device_external_device_id: Option<String>,
        asset_uuid: Option<&String>,
        asset_external_asset_id: Option<&String>,
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        // Use internal new fn with event/stream destinations
        Self::new_data_operation_forwarder(
            Destination::new_event_stream_destinations(
                event_stream_destinations,
                &data_operation_ref.into(),
                asset_uuid,
                asset_external_asset_id,
                &connector_context,
            )?,
            default_destinations,
            device_uuid,
            device_external_device_id,
            data_source,
            data_operation_ref.data_operation_name.clone(),
            data_operation_type_ref,
            connector_context,
        )
    }

    #[allow(clippy::too_many_arguments)]
    fn new_data_operation_forwarder(
        mut data_operation_destinations: Vec<Destination>,
        default_destinations: &[Arc<Destination>],
        device_uuid: Option<String>,
        device_external_device_id: Option<String>,
        data_source: Option<String>,
        data_operation_name: DataOperationName,
        data_operation_type_ref: Option<String>,
        connector_context: Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        // if the data operation has destinations defined, use them, otherwise use the default data operation destinations
        // for now, this vec will only ever be length 1
        let destination = match data_operation_destinations.pop() {
            Some(destination) => ForwarderDestination::DataOperationDestination(destination),
            None => {
                if default_destinations.is_empty() {
                    Err(AdrConfigError {
                                code: None,
                                details: None,
                                // TODO: this may not be true
                                message: Some("Asset must have default data operation destinations if data operation doesn't have destinations".to_string()),
                            })?
                } else {
                    // for now, this vec will only ever be length 1
                    ForwarderDestination::DefaultDestination(default_destinations[0].clone())
                }
            }
        };

        Ok(Self {
            message_schema_reference: None,
            destination,
            device_uuid,
            device_external_device_id,
            data_source,
            data_operation_name,
            data_operation_type_ref,
            connector_context,
        })
    }

    /// Forwards [`Data`] to the destination
    /// Returns once the message has been sent successfully
    /// `protocol_specific_identifier` can be provided to be used when forming Cloud Event Headers
    /// If not specified, fallback fields will be used instead
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
    pub(crate) async fn send_data(
        &self,
        data: Data,
        protocol_specific_identifier: Option<&str>,
    ) -> Result<(), Error> {
        // Forward the data to the destination
        let destination = match &self.destination {
            ForwarderDestination::DefaultDestination(destination) => destination.as_ref(),
            ForwarderDestination::DataOperationDestination(destination) => destination,
        };
        match destination {
            Destination::BrokerStateStore { key } => {
                if self
                    .connector_context
                    .state_store_client
                    .set(
                        key.clone().into(),
                        data.payload,
                        self.connector_context.state_store_timeout,
                        None,
                        state_store::SetOptions {
                            expires: None, // TODO: expiry?
                            ..Default::default()
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
                asset_ref,
                asset_uuid,
                asset_external_asset_id,
                telemetry_sender,
            } => {
                // create MQTT message, setting schema id to response from SR (message_schema_uri)
                let cloud_event = self
                    .build_cloud_event_headers(
                        asset_ref,
                        asset_uuid.as_deref(),
                        asset_external_asset_id.as_deref(),
                        data.timestamp,
                        protocol_specific_identifier,
                    )
                    .map_err(|e| Error(ErrorKind::ValidationError(e)))?;
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
                // passes through user headers and adds custom aio cloud event headers
                message_builder.custom_user_data(Self::add_aio_ref_headers(
                    data.custom_user_data,
                    self.device_uuid.as_deref(),
                    &asset_ref.inbound_endpoint_name,
                    asset_uuid.as_deref(),
                ));
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
    pub(crate) fn update_message_schema_reference(
        &mut self,
        message_schema_reference: Option<adr_models::MessageSchemaReference>,
    ) {
        // Add the message schema URI to the forwarder
        self.message_schema_reference = message_schema_reference;
    }

    fn build_cloud_event_headers(
        &self,
        asset_ref: &AssetRef,
        asset_uuid: Option<&str>,
        asset_external_asset_id: Option<&str>,
        data_timestamp: Option<HybridLogicalClock>,
        protocol_specific_identifier: Option<&str>,
    ) -> Result<telemetry::sender::CloudEvent, String> {
        // TODO: remove once message schema validation is turned back on
        #[allow(clippy::manual_map)]
        let message_schema_uri =
            if let Some(message_schema_reference) = &self.message_schema_reference {
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

        // source
        let source = Self::cloud_event_header_source(
            asset_ref,
            protocol_specific_identifier,
            self.device_uuid.as_deref(),
            self.device_external_device_id.as_deref(),
            self.data_source.as_deref(),
        );
        cloud_event_builder.source(source);

        // event type and subject
        let (event_type, subject) = Self::cloud_event_header_event_and_subject(
            asset_ref,
            &self.data_operation_name,
            self.data_operation_type_ref.as_deref(),
            asset_uuid,
            asset_external_asset_id,
        );
        cloud_event_builder.event_type(event_type);
        cloud_event_builder.subject(CloudEventSubject::Custom(subject));

        // data schema
        if let Some(message_schema_uri) = message_schema_uri {
            cloud_event_builder.data_schema(message_schema_uri);
        }

        // time
        if let Some(hlc) = data_timestamp {
            cloud_event_builder.time(DateTime::<Utc>::from(hlc.timestamp));
        }
        cloud_event_builder.build().map_err(|e| {
            match e {
                // since we specify `source`, all required fields will always be present
                telemetry::sender::CloudEventBuilderError::UninitializedField(_) => {
                    unreachable!()
                }
                // This can be caused by a
                // source that isn't a uri reference
                // data_schema that isn't a valid uri - don't think this is possible since we create it
                telemetry::sender::CloudEventBuilderError::ValidationError(e) => e,
                e => e.to_string(),
            }
        })
    }

    /// Creates the source field for a cloud event header. Format:
    /// ms-aio:<Device-CompoundKey>|<ProtocolSpecificIdentifier>|<Device-externalDeviceId*>|<Device-Name>|[/Sub-Source] (Sub-Source is the dataSource on the event or dataset)
    ///     * Device-externalDeviceId should only be used if different from `DeviceUUID`
    ///     `Device-CompoundKey` currently doesn't exist, but may in the future
    ///     `Sub-Source` is the dataSource on the event or dataset
    fn cloud_event_header_source(
        asset_ref: &AssetRef,
        protocol_specific_identifier: Option<&str>,
        device_uuid: Option<&str>,
        device_external_device_id: Option<&str>,
        data_source: Option<&str>,
    ) -> String {
        let mut source = "ms-aio".to_string();
        let mut device_identifier = None;
        if let Some(protocol_id) = protocol_specific_identifier {
            let trimmed_protocol_id = protocol_id.trim();
            if !trimmed_protocol_id.is_empty()
                && aio_cloud_event::CloudEventFields::Source
                    .validate(
                        &format!("{source}:{trimmed_protocol_id}"),
                        aio_cloud_event::DEFAULT_CLOUD_EVENT_SPEC_VERSION,
                    )
                    .is_ok()
            {
                device_identifier = Some(trimmed_protocol_id);
            }
        }
        if device_identifier.is_none()
            && device_external_device_id != device_uuid
            && let Some(external_id) = &device_external_device_id
        {
            let trimmed_external_id = external_id.trim();
            if !trimmed_external_id.is_empty()
                && aio_cloud_event::CloudEventFields::Source
                    .validate(
                        &format!("{source}:{trimmed_external_id}"),
                        aio_cloud_event::DEFAULT_CLOUD_EVENT_SPEC_VERSION,
                    )
                    .is_ok()
            {
                device_identifier = Some(trimmed_external_id);
            }
        }
        source = format!(
            "{source}:{}",
            device_identifier.unwrap_or(asset_ref.device_name.as_str())
        );
        if let Some(data_source) = &data_source {
            // remove any leading slash since we'll add one in
            let trimmed_data_source = data_source.trim().trim_start_matches('/');
            if !trimmed_data_source.is_empty()
                && aio_cloud_event::CloudEventFields::Source
                    .validate(
                        &format!("{source}/{trimmed_data_source}"),
                        aio_cloud_event::DEFAULT_CLOUD_EVENT_SPEC_VERSION,
                    )
                    .is_ok()
            {
                source = format!("{source}/{trimmed_data_source}");
            }
        }
        source
    }

    /// Creates both the (event type, subject) fields for a cloud event header.
    /// Event type Format: [“DataSet”|”Event”|“Stream”]/<typeref-property-value>
    /// Subject Format:
    ///     <Asset-CompoundKey>|<Asset-ExternalAssetId*>|<AssetName>/<DataSet-Name>|<EventGroup-Name>[/Sub-Subject]
    ///     *Asset-externalAssetId should only be used if different from `AssetUUID`
    ///     `Asset-CompoundKey` currently doesn't exist, but may in the future
    ///     `Sub-Subject` name is the event name
    fn cloud_event_header_event_and_subject(
        asset_ref: &AssetRef,
        data_operation_name: &DataOperationName,
        data_operation_type_ref: Option<&str>,
        asset_uuid: Option<&str>,
        asset_external_asset_id: Option<&str>,
    ) -> (String, String) {
        let (mut event_type, data_operation_name) = match data_operation_name {
            DataOperationName::Dataset { name } => ("DataSet".to_string(), name.clone()),
            DataOperationName::Event {
                name,
                event_group_name,
            } => ("Event".to_string(), format!("{event_group_name}/{name}")),
            DataOperationName::Stream { name } => ("Stream".to_string(), name.clone()),
        };
        if let Some(type_ref) = data_operation_type_ref {
            event_type = format!("{event_type}/{type_ref}");
        }

        let mut asset_identifier = asset_ref.name.as_str(); // fallback value if we don't use asset external asset id
        if asset_external_asset_id != asset_uuid
            && let Some(external_id) = asset_external_asset_id
        {
            let trimmed_external_id = external_id.trim();
            if !trimmed_external_id.is_empty() {
                asset_identifier = trimmed_external_id;
            }
        }
        (
            event_type,
            format!("{asset_identifier}/{data_operation_name}"),
        )
    }

    fn add_aio_ref_headers(
        mut curr_user_data: Vec<(String, String)>,
        device_uuid: Option<&str>,
        inbound_endpoint_name: &str,
        asset_uuid: Option<&str>,
    ) -> Vec<(String, String)> {
        let aio_device_ref = if let Some(device_uuid) = device_uuid {
            format!("ms-aio:{device_uuid}/{inbound_endpoint_name}")
        } else {
            format!("ms-aio:{inbound_endpoint_name}")
        };
        curr_user_data.push(("aiodeviceref".to_string(), aio_device_ref));
        let aio_asset_ref = format!("ms-aio:{}", asset_uuid.unwrap_or_default());
        curr_user_data.push(("aioassetref".to_string(), aio_asset_ref));
        curr_user_data
    }
}

#[derive(Debug)]
#[allow(clippy::large_enum_variant)]
pub(crate) enum ForwarderDestination {
    DefaultDestination(Arc<Destination>),
    DataOperationDestination(Destination),
}

enum DataOperationDestinationDefinition {
    /// Dataset destinations
    Dataset(adr_models::DatasetDestination),
    /// Event or Stream destinations
    EventStream(adr_models::EventStreamDestination),
}

enum DataOperationDestinationDefinitionTarget {
    /// Dataset destination target
    Dataset(adr_models::DatasetTarget),
    /// Event or Stream destination target
    EventStream(adr_models::EventStreamTarget),
}

impl DataOperationDestinationDefinition {
    fn target(&self) -> DataOperationDestinationDefinitionTarget {
        match self {
            DataOperationDestinationDefinition::Dataset(destination) => {
                DataOperationDestinationDefinitionTarget::Dataset(destination.target.clone())
            }
            DataOperationDestinationDefinition::EventStream(destination) => {
                DataOperationDestinationDefinitionTarget::EventStream(destination.target.clone())
            }
        }
    }

    fn configuration(&self) -> &adr_models::DestinationConfiguration {
        match self {
            DataOperationDestinationDefinition::Dataset(destination) => &destination.configuration,
            DataOperationDestinationDefinition::EventStream(destination) => {
                &destination.configuration
            }
        }
    }
}

#[allow(dead_code)]
#[allow(clippy::large_enum_variant)]
pub(crate) enum Destination {
    BrokerStateStore {
        key: String,
    },
    Mqtt {
        qos: Option<QoS>, // these are optional so that we use the defaults from the telemetry::sender if they aren't specified on the data_operation/asset definition
        retain: Option<bool>,
        ttl: Option<u64>,
        asset_ref: AssetRef,
        asset_uuid: Option<String>,
        asset_external_asset_id: Option<String>,
        telemetry_sender: telemetry::Sender<BypassPayload>,
    },
    Storage {
        path: String,
    },
}

impl Destination {
    /// Creates a list of new [`Destination`]s from a list of [`adr_models::DatasetDestination`]s.
    /// At this time, this list cannot have more than one element. If there are no items in the list,
    /// this function will return an empty Vec. This isn't an error, since a default destination may or
    /// may not exist in the definition.
    ///
    /// # Errors
    /// [`AdrConfigError`] if the destination is `Mqtt` and the topic is invalid.
    /// This can be used to report the error to the ADR service on the status
    pub(crate) fn new_dataset_destinations(
        dataset_destinations: &[adr_models::DatasetDestination],
        asset_ref: &AssetRef,
        asset_uuid: Option<&String>,
        asset_external_asset_id: Option<&String>,
        connector_context: &Arc<ConnectorContext>,
    ) -> Result<Vec<Self>, AdrConfigError> {
        // Create a new forwarder
        if dataset_destinations.is_empty() {
            Ok(vec![])
        } else {
            // for now, this vec will only ever be length 1
            let definition_destination = &dataset_destinations[0];
            let destination = Self::new_data_operation_destination(
                &DataOperationDestinationDefinition::Dataset(definition_destination.clone()),
                asset_ref,
                asset_uuid,
                asset_external_asset_id,
                connector_context,
            )?;
            Ok(vec![destination])
        }
    }

    /// Creates a list of new [`Destination`]s from a list of [`adr_models::EventStreamDestination`]s.
    /// At this time, this list cannot have more than one element. If there are no items in the list,
    /// this function will return an empty Vec. This isn't an error, since a default destination may or
    /// may not exist in the definition.
    ///
    /// # Errors
    /// [`AdrConfigError`] if the destination is `Mqtt` and the topic is invalid.
    /// This can be used to report the error to the ADR service on the status
    pub(crate) fn new_event_stream_destinations(
        event_stream_destinations: &[adr_models::EventStreamDestination],
        asset_ref: &AssetRef,
        asset_uuid: Option<&String>,
        asset_external_asset_id: Option<&String>,
        connector_context: &Arc<ConnectorContext>,
    ) -> Result<Vec<Self>, AdrConfigError> {
        // Create a new forwarder
        if event_stream_destinations.is_empty() {
            Ok(vec![])
        } else {
            // for now, this vec will only ever be length 1
            let definition_destination = &event_stream_destinations[0];
            let destination = Self::new_data_operation_destination(
                &DataOperationDestinationDefinition::EventStream(definition_destination.clone()),
                asset_ref,
                asset_uuid,
                asset_external_asset_id,
                connector_context,
            )?;
            Ok(vec![destination])
        }
    }

    fn new_data_operation_destination(
        data_operation_destination_definition: &DataOperationDestinationDefinition,
        asset_ref: &AssetRef,
        asset_uuid: Option<&String>,
        asset_external_asset_id: Option<&String>,
        connector_context: &Arc<ConnectorContext>,
    ) -> Result<Self, AdrConfigError> {
        Ok(match data_operation_destination_definition.target() {
            DataOperationDestinationDefinitionTarget::Dataset(
                adr_models::DatasetTarget::BrokerStateStore,
            ) => {
                Destination::BrokerStateStore {
                    // TODO: validate key not empty?
                    key: data_operation_destination_definition
                        .configuration()
                        .key
                        .clone()
                        .expect("Key must be present if Target is BrokerStateStore"),
                }
            }
            DataOperationDestinationDefinitionTarget::EventStream(
                adr_models::EventStreamTarget::Mqtt,
            )
            | DataOperationDestinationDefinitionTarget::Dataset(adr_models::DatasetTarget::Mqtt) => {
                let telemetry_sender_options = telemetry::sender::OptionsBuilder::default()
                    .topic_pattern(
                        data_operation_destination_definition
                            .configuration()
                            .topic
                            .clone()
                            .expect("Topic must be present if Target is Mqtt"),
                    )
                    .build()
                    // TODO: check if this can fail, or just the next one
                    .map_err(|e| AdrConfigError {
                        code: None,
                        details: None,
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
                    message: Some(e.to_string()),
                })?;
                Destination::Mqtt {
                    qos: data_operation_destination_definition
                        .configuration()
                        .qos
                        .map(Into::into),
                    retain: data_operation_destination_definition
                        .configuration()
                        .retain
                        .as_ref()
                        .map(|r| matches!(r, adr_models::Retain::Keep)),
                    ttl: data_operation_destination_definition.configuration().ttl,
                    asset_ref: asset_ref.clone(),
                    asset_uuid: asset_uuid.cloned(),
                    asset_external_asset_id: asset_external_asset_id.cloned(),
                    telemetry_sender,
                }
            }
            DataOperationDestinationDefinitionTarget::EventStream(
                adr_models::EventStreamTarget::Storage,
            )
            | DataOperationDestinationDefinitionTarget::Dataset(
                adr_models::DatasetTarget::Storage,
            ) => {
                Err(AdrConfigError {
                    code: None,
                    details: None,
                    message: Some(
                        "Storage destination not supported for this connector".to_string(),
                    ),
                })?
                // Destination::Storage {
                //     path: definition_destination
                //         .configuration
                //         .path
                //         .expect("Path must be present if Target is Storage"),
                // }
            }
        })
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
                asset_ref,
                asset_uuid,
                asset_external_asset_id,
                telemetry_sender: _,
            } => f
                .debug_struct("Mqtt")
                .field("qos", qos)
                .field("retain", retain)
                .field("ttl", ttl)
                .field("asset_ref", asset_ref)
                .field("asset_uuid", asset_uuid)
                .field("asset_external_asset_id", asset_external_asset_id)
                // .field("telemetry_sender", telemetry_sender)
                .finish(),
            Self::Storage { path } => f.debug_struct("Storage").field("path", path).finish(),
        }
    }
}

#[cfg(test)]
mod tests {

    use test_case::{test_case, test_matrix};

    use super::*;

    fn asset_ref() -> AssetRef {
        AssetRef {
            name: "asset_name".to_string(),
            device_name: "device_name".to_string(),
            inbound_endpoint_name: "inbound_endpoint_name".to_string(),
        }
    }

    #[test_matrix([Some("device-uuid"), None],
                  [Some("external-device-id"), Some("device-uuid"), None])]
    fn cloud_event_header_source_with_protocol_specific_identifier_and_data_source(
        device_uuid: Option<&str>,
        device_external_device_id: Option<&str>,
    ) {
        let asset_ref = asset_ref();
        let source = Forwarder::cloud_event_header_source(
            &asset_ref,
            Some("protocol123"),
            device_uuid,
            device_external_device_id,
            Some("data_source"),
        );
        assert_eq!(source, "ms-aio:protocol123/data_source");
    }

    #[allow(clippy::unnecessary_owned_empty_strings)] // needed because of test_matrix macro that treats " " and "" the same
    #[test_matrix([Some("device-uuid"), None],
                  [Some("external-device-id"), Some("device-uuid"), None],
                [None, Some(&String::new()), Some(" "), Some("not valid uri")])]
    fn cloud_event_header_source_no_valid_data_source(
        device_uuid: Option<&str>,
        device_external_device_id: Option<&str>,
        data_source: Option<&str>,
    ) {
        let asset_ref = asset_ref();
        let source = Forwarder::cloud_event_header_source(
            &asset_ref,
            Some("protocol123"),
            device_uuid,
            device_external_device_id,
            data_source,
        );
        assert_eq!(source, "ms-aio:protocol123");
    }

    #[allow(clippy::unnecessary_owned_empty_strings)] // needed because of test_matrix macro that treats " " and "" the same
    #[test_matrix([None, Some(&String::new()), Some(" "), Some("not valid ?!#$# url")], [Some("device-uuid"), Some(" "), None])]
    fn cloud_event_header_source_uses_external_device_id(
        protocol_specific_identifier: Option<&str>,
        device_uuid: Option<&str>,
    ) {
        let asset_ref = asset_ref();
        let source = Forwarder::cloud_event_header_source(
            &asset_ref,
            protocol_specific_identifier,
            device_uuid,
            Some("external_device_id"),
            Some("data_source"),
        );
        assert_eq!(source, "ms-aio:external_device_id/data_source");
    }

    #[allow(clippy::unnecessary_owned_empty_strings)] // needed because of test_matrix macro that treats " " and "" the same
    #[test_matrix([None, Some(&String::new()), Some(" "), Some("not valid ?!#$# url")],
    [Some("device-uuid"), None, Some(" "), Some(&String::new())])]
    fn cloud_event_header_source_uses_device_name(
        protocol_specific_identifier: Option<&str>,
        external_device_id: Option<&str>,
    ) {
        let asset_ref = asset_ref();
        let source = Forwarder::cloud_event_header_source(
            &asset_ref,
            protocol_specific_identifier,
            Some("device-uuid"),
            external_device_id,
            Some("data_source"),
        );
        assert_eq!(source, "ms-aio:device_name/data_source");
    }

    #[test_case(&DataOperationName::Dataset{name: "dataset_name".to_string()}; "dataset")]
    #[test_case(&DataOperationName::Dataset{name: "other".to_string()}; "different_dataset_name_same_result")]
    fn cloud_event_header_event_dataset_no_type_ref(data_operation_name: &DataOperationName) {
        let asset_ref = asset_ref();
        let (event, _) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            data_operation_name,
            None,
            None,
            None,
        );
        assert_eq!(event, "DataSet");
    }

    #[test_case(&DataOperationName::Dataset{name: "dataset_name".to_string()}; "dataset")]
    #[test_case(&DataOperationName::Dataset{name: "other".to_string()}; "different_dataset_name_same_result")]
    fn cloud_event_header_event_dataset_with_type_ref(data_operation_name: &DataOperationName) {
        let asset_ref = asset_ref();
        let (event, _) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            data_operation_name,
            Some("type-ref-value"),
            None,
            None,
        );
        assert_eq!(event, "DataSet/type-ref-value");
    }

    #[test_case(&DataOperationName::Stream{name: "stream_name".to_string()}; "stream")]
    #[test_case(&DataOperationName::Stream{name: "other".to_string()}; "different_stream_name_same_result")]
    fn cloud_event_header_event_stream_no_type_ref(data_operation_name: &DataOperationName) {
        let asset_ref = asset_ref();
        let (event, _) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            data_operation_name,
            None,
            None,
            None,
        );
        assert_eq!(event, "Stream");
    }

    #[test_case(&DataOperationName::Stream{name: "stream_name".to_string()}; "stream")]
    #[test_case(&DataOperationName::Stream{name: "other".to_string()}; "different_stream_name_same_result")]
    fn cloud_event_header_event_stream_with_type_ref(data_operation_name: &DataOperationName) {
        let asset_ref = asset_ref();
        let (event, _) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            data_operation_name,
            Some("type-ref-value"),
            None,
            None,
        );
        assert_eq!(event, "Stream/type-ref-value");
    }

    #[test_case(&DataOperationName::Event{name: "event_name".to_string(), event_group_name: "event_group_name".to_string()}; "event")]
    #[test_case(&DataOperationName::Event{name: "other".to_string(), event_group_name: "other2".to_string()}; "different_event_name_same_result")]
    fn cloud_event_header_event_event_no_type_ref(data_operation_name: &DataOperationName) {
        let asset_ref = asset_ref();
        let (event, _) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            data_operation_name,
            None,
            None,
            None,
        );
        assert_eq!(event, "Event");
    }

    #[test_case(&DataOperationName::Event{name: "event_name".to_string(), event_group_name: "event_group_name".to_string()}; "event")]
    #[test_case(&DataOperationName::Event{name: "other".to_string(), event_group_name: "other2".to_string()}; "different_event_name_same_result")]
    fn cloud_event_header_event_event_with_type_ref(data_operation_name: &DataOperationName) {
        let asset_ref = asset_ref();
        let (event, _) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            data_operation_name,
            Some("type-ref-value"),
            None,
            None,
        );
        assert_eq!(event, "Event/type-ref-value");
    }

    #[test_case(&DataOperationName::Dataset{name: "dataset_name".to_string()}, "asset_name/dataset_name"; "dataset")]
    #[test_case(&DataOperationName::Stream{name: "stream_name".to_string()}, "asset_name/stream_name"; "stream")]
    #[test_case(&DataOperationName::Event{name: "event_name".to_string(), event_group_name: "event_group_name".to_string()}, "asset_name/event_group_name/event_name"; "event")]
    #[test_case(&DataOperationName::Event{name: "other".to_string(), event_group_name: "other2".to_string()}, "asset_name/other2/other"; "other_event")]
    fn cloud_event_header_subject(data_operation_name: &DataOperationName, result_subject: &str) {
        let asset_ref = asset_ref();
        let (_, subject) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            data_operation_name,
            None,
            None,
            None,
        );
        assert_eq!(subject, result_subject);
    }

    #[test_case(Some("asset-uuid"); "uuid and asset id don't match")]
    #[test_case(Some(" "); "uuid is whitespace")]
    #[test_case(Some(""); "uuid is empty")]
    #[test_case(None; "uuid doesn't exist")]
    fn cloud_event_header_subject_uses_external_asset_id(asset_uuid: Option<&str>) {
        let asset_ref = asset_ref();
        let (_, subject) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            &DataOperationName::Dataset {
                name: "dataset_name".to_string(),
            },
            None,
            asset_uuid,
            Some("external_asset_id"),
        );
        assert_eq!(subject, "external_asset_id/dataset_name");
    }

    #[test_case(Some("asset-uuid"); "uuid and asset id match")]
    #[test_case(Some(" "); "asset id is whitespace")]
    #[test_case(Some(""); "asset id is empty")]
    #[test_case(None; "asset id doesn't exist")]
    fn cloud_event_header_subject_uses_asset_name(external_asset_id: Option<&str>) {
        let asset_ref = asset_ref();
        let (_, subject) = Forwarder::cloud_event_header_event_and_subject(
            &asset_ref,
            &DataOperationName::Dataset {
                name: "dataset_name".to_string(),
            },
            None,
            Some("asset-uuid"),
            external_asset_id,
        );
        assert_eq!(subject, "asset_name/dataset_name");
    }
}
