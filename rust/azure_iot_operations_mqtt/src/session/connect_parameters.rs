// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::Duration;

use azure_mqtt::client::ConnectionTransportConfig;
use bytes::Bytes;

use crate::control_packet::ConnectProperties;

struct ConnectParameters {
    pub initial_clean_start: bool,
    pub keep_alive: azure_mqtt::client::KeepAliveConfig,
    pub will: Option<azure_mqtt::packet::Will>,         // TODO: expose as re-export
    pub username: Option<String>,
    pub password: Option<Bytes>,
    pub connect_properties: ConnectProperties,
    pub connect_timeout: Duration,
}

impl ConnectParameters {
    // // TODO: return a real error instead of String
    // pub fn connection_transport_config(&self) -> Result<ConnectionTransportConfig, String> {

    // }
}