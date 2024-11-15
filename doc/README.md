# Azure IoT Operations SDKs

Here you find will detailed information on what the SDKs are, how they were constructed, and what edge applications they can be used to create.

Multiple languages are supported, with each language provides an SDK (collectively known as the *Azure IoT Operations SDKs*) with the same level of features and support available in each. The languages supported today are C#, Go and Rust, however additional languages will be added based on customer demand.

## Goals

The goals of the Azure IoT Operations SDKS is to provide an application framework to abstract the MQTT concepts, with a clean API, that can also be consumed using _Codegen_ from DTDL models.

The SDKs can be used to build highly available applications at the edge, that interact with Azure IoT Operations to perform operations such as **asset discovery**, **protocol translation** and **data transformation**.

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

## Components

The Azure IoT Operations SDKs provide a number of components available for customers:

* A **Session client**, that augments the MQTT client, adding reconnection and authentication to provide a seemless connectivity experience.

* A set of protocol primitives, designed to assist in creating applications, built on the fundamental protocol implementations; **Commands** and **Telemetry**. 

* A set of clients providing integration with **Azure IoT Operations services** such as **State Store**, **Leader Election**, **Leased Lock**, and **Schema Registry**.

* The **Protocol Compiler (Codegen)** allows clients and servers to communicate via a schema contract. First describe the communication (using **Telemetry** and **Commands**) with DTDL, then generate a set of client libraries and server library stubs across the supported programming languages.

Read further about the underlying terminology and different components of the SDKs:

* [Terminology](terminology.md) - Understand the different terms used to describe the concepts and construction of the SDKs.
* [Components](components.md) - An outline of each component and their function.

## Applications types

The SDK supports the following application types:

| Application type | Description |
|-|-|
| [Edge application](edge_application) | A generic edge application that needs to interface with various Azure IoT Operations services such as the MQTT broker and state store. The SDKs provides convenient clients to simplify the development experience. </br>*An Edge Application is a customer managed artifact, including deployment to the cluster and monitor execution.* |
|
| [Akri connector](akri_connector) in development| A specialized edge application deployed by the Akri Operator and designed to interface with on-premises asset endpoints. The Akri connector is responsible for discovering assets available on the endpoint, and relaying information to and from those assets.</br>*The Akri Connector's deployment is managed automatically by the Akri Operator.* |

> [!NOTE]
> The Akri connector is part of the Akri service, which is under active development and will be available soon.

## Developing applications

1. Read more on developing [edge applications](edge_application).

1. Review the [samples](/samples) directory for samples and tutorials.

1. Learn how to [deploy](deploy.md) your application to the cluster.

## Limitations

Review any [known limitations](limitations.md) associated with the current service and client implementations.

## Reference

Read the reference information about the fundamentals primitives and protocols and that make up the SDKs.

1. [Reference](reference)
