// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use std::time::Duration;

use bytes::Bytes;

use crate::control_packet::ConnectProperties;

struct ConnectParameters {
    initial_clean_start: bool,
    keep_alive: azure_mqtt::client::KeepAliveConfig,
    will: Option<azure_mqtt::packet::Will>,         // TODO: expose as re-export
    username: Option<String>,
    password: Option<Bytes>,
    connect_properties: ConnectProperties,
    connect_timeout: Duration,
}
