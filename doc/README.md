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

## Applications types

The SDK supports the following application types:

| Application type | Description |
|-|-|
| [Edge Application](edge_application) | A generic edge application that needs to interface with various Azure IoT Operations services such as the MQTT broker and state store. The SDKs provides convenient clients to simplify the development experience. </br>*An Edge Application is a customer managed artifact, including deployment to the cluster and monitor execution.* |
| [Akri Connector](akri_connector) | A specialized edge application deployed by the Akri Operator and designed to interface with on-premises asset endpoints. The Akri connector is responsible for discovering assets available on the endpoint, and relaying information to and from those assets.</br>*The Akri Connector's deployment is managed automatically by the Akri Operator.* |

> [!NOTE]
> The Akri connector is part of the Akri service, which is under active development and currently not available for use.

## Developing applications

1. Review the various SDKs and tools:
   * [.NET](/dotnet)
   * [Go](/go)
   * [Rust](/rust)
   * [Protocol compiler](/codegen)

1. Check out the [samples](/samples) for samples and tutorials across the SDKs.

1. Learn how to [deploy](deploy.md) your application to the cluster.

## Reference

Read the reference information about the fundamentals primitives and protocols and that make up the SDKs.

1. [Reference documentation](reference)
