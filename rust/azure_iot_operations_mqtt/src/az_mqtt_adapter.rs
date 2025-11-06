// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Adapter layer for the az mqtt crate

use std::{fmt, fs, time::Duration};

use async_trait::async_trait;
use bytes::Bytes;
use openssl::{pkey::PKey, x509::X509};
use thiserror::Error;

use crate::connection_settings::MqttConnectionSettings;
use crate::control_packet::{
    AuthProperties, Publish, PublishProperties, QoS, SubscribeProperties, UnsubscribeProperties,
};
use crate::error::{
    AckError, AckErrorKind, ConnectionError, DisconnectError, DisconnectErrorKind, PublishError,
    PublishErrorKind, ReauthError, ReauthErrorKind, SubscribeError, SubscribeErrorKind,
    UnsubscribeError, UnsubscribeErrorKind,
};
use crate::topic::{TopicFilter, TopicName};

pub type ClientAlias = azure_mqtt::client::Client;

/// Client constructors + TLS
/// -------------------------------------------
pub fn client(
    // connection_settings: MqttConnectionSettings,
    client_options: azure_mqtt::ClientOptions,
    channel_capacity: usize,
    manual_ack: bool,
    connection_user_properties: Vec<(String, String)>,
) -> Result<
    (
        azure_mqtt::AsyncClient,
        azure_mqtt::ConnectHandle,
        azure_mqtt::Receiver,
    ),
    MqttAdapterError,
> {
    // let (client_options, _, _) = connection_settings.into();
    // NOTE: channel capacity for AsyncClient must be less than usize::MAX - 1 due to (presumably) a bug.
    // It panics if you set MAX, although MAX - 1 is fine.
    if channel_capacity == usize::MAX {
        return Err(MqttAdapterError::Other(
            "rumqttc does not support channel capacity of usize::MAX".to_string(),
        ));
    }
    // let (client_options, _, _) = connection_settings.try_into()?;
    // mqtt_options.set_manual_acks(manual_ack);

    // Add any provided user properties
    // let mut existing_props = mqtt_options.user_properties();
    // existing_props.extend(connection_user_properties);
    // mqtt_options.set_user_properties(existing_props);

    Ok(azure_mqtt::new_client(client_options))
}
