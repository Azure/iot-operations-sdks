// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//! Client for Observability operations.
//!
//! To use this client, the `observality` feature must be enabled.

use std::collections::HashMap;

use azure_iot_operations_mqtt::interface::ManagedClient;
use azure_iot_operations_protocol::application::ApplicationContext;

// use crate::observability::observability_gen::akri_observability_service_metrics_apis::service::TelemetrySender;

use super::{
    CLIENT_ID_TOKEN, observability_gen::common_types::common_options::TelemetryOptionsBuilder,
};

/// Observability client implementation.
pub struct Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    observability_sender: TelemetrySender<C>,
}

impl<C> Client<C>
where
    C: ManagedClient + Clone + Send + Sync + 'static,
    C::PubReceiver: Send + Sync,
{
    pub fn new(application_context: ApplicationContext, client: &C) -> Self {
        let options = TelemetryOptionsBuilder::default()
            .topic_token_map(HashMap::from([(
                CLIENT_ID_TOKEN.to_string(), // FIN: Figure out if this is correct.
                client.client_id().to_string(),
            )]))
            .build()
            .unwrap();

        Self {
            observability_sender: TelemetrySender::new(
                application_context,
                client.clone(),
                &options,
            ),
        }
    }
}
