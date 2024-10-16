# Azure IoT Operations SDKs

Here you find will detailed information on what the SDKs are, how they were constructed, and what edge applications they can be used to create.

Multiple languages are supported, with each language provides an SDK (collectively known as the *Azure IoT Operations SDKs*) with the same level of features and support available in each. The languages supported today are C#, Go and Rust, however additional languages will be added based on customer demand.

## Goals

The goals of the Azure IoT Operations SDKS is to provide an application framework to abstract the MQTT concepts, with a clean API, that can also be consumed using _Codegen_ from DTDL models.

The SDKs can be used to build highly available applications at the edge, that interact with Azure IoT Operations to perform operations such as **asset discovery**, **protocol translation** and **data transformation**.

## Components

Read further about the underlying terminology and different components of the SDKs:

* [Terminology](terminology.md) - Understand the different terms used to describe the concepts and construction of the SDKs.
* [Components](components.md) - An outline of each of the client libraries, and their function.

## Limitations

Review any [known limitations](limitations.md) associated with the current service and client implementations.

## Benefits

The SDKs provide a number of benefits compared to utilizing the MQTT client directly:

| Feature | Benefit |
|-|-|
| **Connectivity** | Maintain a secure connection to the MQTT Broker, including rotating server certificates and authentication keys |
| **Security** | Support SAT or x509 certificate authentication with credential rotation |
| **Configuration** | Configure the Broker connection through the file system, environment or connection string |
| **Services** | Provides client libraries to Azure IoT Operation services for simplified development |
| **Codegen** | Provides contract guarantees between client and servers via RPC and telemetry |
| **High availability** | Building blocks for building HA apps via State Store, Lease Lock and Leader Election clients |
| **Payload formats** | Supports multiple serialization formats, built in |

## Layering

The Azure IoT Operations SDKs provide a number of layers for a customer to engage on:

1. A set of primitive libraries, designed to assist customers in creating applications built on the fundamental protocol implementations, **RPC** and **Telemetry**. 

1. A session client, that augments the MQTT client, adding reconnection and authentication to provide a seemless connectivity experience.

1. A set of clients implementing integration with **Azure IoT Operations services** such as **State Store**, **Leader Election**, **Leased Lock**, and **Schema Registry**.

1. The Protocol Compiler allows clients and servers to communicate via a schema contract. Describe the communication (Telemetry, RPC and serialization) using DTDL, then generate a set of client libraries and server library stubs across a set of popular programming languages.

## Samples and Tutorials

See the [samples](/samples) directory for links to available samples and tutorials across the SDKs.

## Reference

Reference information about the fundamentals primitives and protocols and that make up the SDKs.

1. [Reference documentation](reference)
