# Azure.Iot.Operations.Connector

## Overview

This package contains classes to facilitate the creation of Azure IoT Operation (aka "AIO") connector applications. 

AIO connector applications allow for you to connect services (HTTP servers, SQL servers, TCP endpoints, etc.) to the AIO ecosystem. For 
instance, you could write a connector that periodically updates the AIO broker state store with some state taken from your SQL server. 
Alternatively, you could listen for telemetry from a TCP server and forward them as MQTT telemetry to the AIO MQTT broker.

## Samples

To see sample connectors that demonstrate using this package, please see here (TODO link)

## Feedback

To share feedback or file issues, please see [here](https://github.com/Azure/iot-operations-sdks/issues)