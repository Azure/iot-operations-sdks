# Azure.Iot.Operations.Protocol

## Overview

This package contains definitions for two MQTT-based communication patterns:

Remote Procedure Call (aka "RPC")
 - Request + response communication with support for custom user payload types and user properties

Telemetry
 - Unidirectional communication with support for custom user payload types and user properties

This package also includes an MQTT client interface definition that allows users to perform RPC + telemetry with the .NET MQTT client of their choice. The Azure.Iot.Operations.Mqtt package contains the implementation of this interface using the MQTTnet package, so users may use these two packages together to perform RPC + telemetry.

The communication patterns defined in this package can be used with the MQTT broker in an Azure IoT Operations deployment and with other MQTT brokers.

## Samples

To see samples that demonstrate basic RPC + telemetry, please see [here](https://github.com/Azure/iot-operations-sdks/tree/main/dotnet/samples/Protocol)

## Feedback

To share feedback or file issues, please see [here](https://github.com/Azure/iot-operations-sdks/issues)