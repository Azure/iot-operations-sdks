# Azure IoT Operations - Services

Utilities for using the Azure IoT Operations Services over MQTT, leveraging the [Azure IoT Operations - MQTT](../azure_iot_operations_mqtt/) crate.
 
[API documentation](https://azure.github.io/iot-operations-sdks/rust/azure_iot_operations_services) |
[Examples](examples) |
[Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=rust%2Fservices&expanded=true)
 
## Overview
 
The Azure IoT Operations Services provides clients for the various services in Azure IoT Operations:
 
- State Store
- Schema Registry
- Leased Lock
- Leader Election (coming soon)

## Features

To enable a specific client, the corresponding feature must be enabled.
- `all`: Enables all clients.
- `state_store`: Enables the State Store client.
- `schema_registry`: Enables the Schema Registry client.
- `leased_lock`: Enables the Leased Lock client.
