# Azure.Iot.Operations.Mqtt

## Overview

This package contains implementations for the MQTT client interface defined in the Azure.Iot.Operations.Protocol package using the MQTTnet client library.

There are two implementations available within this package:
 - MqttSessionClient
	- This is a session-aware MQTT client that includes automatic reconnection and allows for queueing of outgoing PUBLISH/SUBSCRIBE/UNSUBSCRIBE packets even during reconnection 
 - OrderedAckMqttClient
	- This implementation is a bare-bones wrapper of the existing MQTTnet client that only adds automatic ordering of outgoing PUBACKs.

The clients defined in this library can be used with the MQTT broker in an Azure IoT Operations deployment and with other MQTT brokers.

## Samples

To see samples that demonstrate using these clients, please see [here](https://github.com/Azure/iot-operations-sdks/tree/main/dotnet/samples/Mqtt)

## Feedback

To share feedback or file issues, please see [here](https://github.com/Azure/iot-operations-sdks/issues)