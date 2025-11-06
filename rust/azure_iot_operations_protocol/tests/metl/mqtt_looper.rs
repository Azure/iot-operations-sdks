// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

use async_trait::async_trait;
use azure_iot_operations_mqtt::error::ConnectionError;
use azure_iot_operations_mqtt::interface::{Event, MqttEventLoop};
use bytes::Bytes;
use tokio::sync::mpsc;

pub struct MqttLooper {
    event_rx: Option<mpsc::UnboundedReceiver<Result<Event, ConnectionError>>>,
}

impl MqttLooper {
    pub fn new(event_rx: Option<mpsc::UnboundedReceiver<Result<Event, ConnectionError>>>) -> Self {
        Self { event_rx }
    }
}

#[async_trait]
impl MqttEventLoop for MqttLooper {
    async fn poll(&mut self) -> Result<Event, ConnectionError> {
        match self
            .event_rx
            .as_mut()
            .expect("MqttEventLoop::poll() called but MQTT emulation is not at Event level")
            .recv()
            .await
        {
            Some(event) => event,
            None => Err(ConnectionError::RequestsDone),
        }
    }

    fn set_clean_start(&mut self, _clean_start: bool) {}

    fn set_authentication_method(&mut self, _authentication_method: Option<String>) {}

    fn set_authentication_data(&mut self, _authentication_data: Option<Bytes>) {}
}
