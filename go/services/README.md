# services

| [Samples](../samples/services) |
[Release Notes](https://github.com/Azure/iot-operations-sdks/releases?q=go%2Fservices)
|

## Overview

This module provides clients for the services provided by Azure IoT Operations.

-   [State Store](statestore/API.md) - A distributed storage system which offers
    the same high availability guarantees as MQTT messages in MQTT broker. This
    client provides the common CRUD operations as well as the ability to observe
    keys for changes. See the [MQTT broker state store protocol][1] for more
    details.
-   [Leased Lock](leasedlock/API.md) - A distributed locking mechanism using
    [fencing tokens][2] based on (and for use with) the state store.
-   [Schema Registry](schemaregistry/API.md) - A client for the schema registry
    to fetch and store asset schemas.

Note that, unlike the other modules in this repo, this module is specific to the
Azure IoT Operations and will not work with other MQTTv5 brokers.

[1]:
    https://learn.microsoft.com/en-us/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol#state-store-protocol-overview
[2]:
    https://learn.microsoft.com/en-us/azure/iot-operations/create-edge-apps/concept-about-state-store-protocol#locking-and-fencing-tokens
