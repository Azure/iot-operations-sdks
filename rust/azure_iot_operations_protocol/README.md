# Azure IoT Operations - Protocol
Utilities for using the Azure IoT Operations Protocol over MQTT, leveraging the [Azure IoT Operations - MQTT](../azure_iot_operations_mqtt/) crate.

[API documentation] |
[Examples](examples) |
[Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=rust%2Fprotocol&expanded=true)

## Overview

The Azure IoT Operations Protocol allows for structured data to be sent and received between applications in two patterns:

- RPC Command - Send requests, process them, and respond
- Telemetry (Coming Soon) - Send and receive telemetry messages

Simply implement the provided serialization traits for your structured data, and use the envoy clients for the pattern you wish to use!
