# Overview

## Goals

Provide an application framework to abstract the MQTT concepts with a clean API, that can also be consumed using _Codegen_ from DTDL models.

## Layering of SDKs

The Azure IoT Operations SDKs will be available to customers in three ways:

1. Samples and guidance, where samples will use standard MQTT5 features and work on any broker that is MQTT5 compliant.

1. A collection of documented libraries, to help customers create applications using the fundamental protocol implementations (RPC + Telemetry). Libraries will be usable stand-alone, or via DTDL modeling and Codegen.

1. A collection of clients built on the above libraries. Clients are built on MQTT5 and implement advanced client features such as Store Store, Leader Election, Schema Registry, etc. 

## Testing Strategy