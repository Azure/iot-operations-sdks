// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Types related to Azure IoT Operations concepts and specifications, which can be used
//! with the MQTT client in this crate.

pub mod connection_settings;

/// Options for configuring features on a [`Session`](crate::session::Session) that are specific to the AIO broker
#[derive(Builder)]
pub struct AIOBrokerFeatures {
    /// Indicates if the Session should use AIO persistence
    #[builder(default = "false")]
    pub(crate) persistence: bool,
}
