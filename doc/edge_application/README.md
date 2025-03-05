# Edge application development

The Azure IoT Operations SDKs directly support building edge applications in a number of ways:

1. Provides a MQTT `session client` enabling simple secure connectivity to MQTT broker including automatic online credential renewal and session resuming on reconnection to minimum message loss.

1. The `state store client` provides a clean interface to get, set and observe key-values.

1. The `telemetry client` for sending and receiving telemetry message.

1. The `command client` for invoking and executing commands using RPC over MQTT.

The following overview the various components that an edge application will interact with:

![alt text](images/edge-applications.png)

## Setup

Developing an edge application requires a Kubernetes cluster with Azure IoT Operations deployed. In additional, the MQTT broker should be configured to allow access from off cluster.

> [!TIP]
> 
> Follow the [setup instructions](../setup.md) to setup your development environment and configure Azure IoT Operations.

## Creating your first application

Once your development environment is setup, its time to create your first application. The first step is to choose your preferred language. Currently the SDKs are available in .NET, Go and Rust.

Refer to each language directory for instructions on setting up for that particular language:

* **.NET** SDK - [/dotnet](/dotnet)
* **Go** SDK - [/go](/go)
* **Rust** SDK - [/rust](/rust)
