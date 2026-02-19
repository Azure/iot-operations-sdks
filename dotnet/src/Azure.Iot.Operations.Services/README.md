# Azure.Iot.Operations.Services

## Overview

This package contains client objects for interacting with the various services that come with an Azure Iot Operations (aka "AIO") deployment including:

- StateStoreClient
  - The client for interacting with AIO's distributed state store. This is a centralized key-value store accessible to any client connected to the AIO deployment's MQTT broker. It allows for sharing of keys/values across multiple applications and includes broker-side persistence.
- SchemaRegistryClient
  - The client for interacting with AIO's schema registry service. This service allows for the registration and retrieval of message schemas. These message schemas are typically used to communicate how to deserialize payloads received from other applications connected to the AIO MQTT broker. 
- LeasedLockClient/LeaderElectionClient
  - Clients that use the AIO deployment's broker state store to perform leased lock and leader election operations.
	- For example, these clients facilitate protecting shared resources in the broker state store across multiple applications connected to the AIO MQTT broker 
- Asset and Device Registry Clients
  - These clients allow for interacting with AIO's Akri service. This is usually only relevant when developing an AIO connector and the Azure.Iot.Operations.Connector package abstracts these clients to make it easier to develop connectors, but they can be used directly.


The clients in this package are designed to work specifically with the MQTT broker found in an AIO deployment. They cannot be used against other MQTT brokers.

The clients in this package can be used even outside of the context of an AIO connector, though.

## Samples

To see samples that demonstrate these clients, please see [here](https://github.com/Azure/iot-operations-sdks/tree/main/dotnet/samples/Services).

## Feedback

 To share feedback or file issues, please see [here](https://github.com/Azure/iot-operations-sdks/issues)