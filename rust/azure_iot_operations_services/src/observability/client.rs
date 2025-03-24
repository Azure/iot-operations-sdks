// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Observability operations.
//!
//! To use this client, the `observality` feature must be enabled.

use azure_iot_operations_mqtt::interface::ManagedClient;

use crate::observability::observability_gen::akri_observability_service_metrics_apis::service::TelemetrySender;

/// Observability client implementation.
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    observability_sender: TelemetrySender<C>,
}
