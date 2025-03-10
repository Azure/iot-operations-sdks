# .NET Samples

> [!CAUTION]
>
> The samples and tutorials provided in this repository are for **demonstration purposed only**, and should not be deployed to a production system or included within an application without a full understanding of how they operate.
>
> Additionally some samples may use a username and password for authentication. This is used for sample simplicity and a production system should use a robust authentication mechanism such as certificates.
>
> To learn more about securing your edge solution, refer to [Security best practices for IoT solutions](https://learn.microsoft.com/azure/iot/iot-overview-security).

This directory contains a variety of samples demonstrating how to use the .NET packages to interact with Azure IoT Operations.

## Setup your environment

Follow the [setup](/doc/setup.md) directions to prepare an Azure IoT Operations cluster for development.

## Run the samples

### MQTT connection

1. [Session Client](./SessionClientConnectionManagementSample/) - Create an MQTT session using the Session Client
1. [User Managed Connection](./UserManagedConnectionManagementSample/) - Create your MQTT client and connect to the MQTT broker

### Services

1. [State store](./StateStoreClientSample/) - Set, get and delete a key
1. [State store observe](./StateStoreObserveKeySample/) - Be notified of changes to an observed key
1. [Leased lock](./LeasedLockSample/) - Lock a key in the state store shared between applications
1. [Passive replication](./PassiveReplicationSample/) - Use the leader election client to perform passive replication
1. [Schema registry](./SchemaRegistrySample/) - Put and get schemas in the schema registry

### Telemetry and RPC

1. [Cloud events](./SampleCloudEvents/) - Send telemetry with cloud events referencing the payload schema
1. [Read cloud events](./SampleReadCloudEvents/) - Receive telemetry with cloud events and fetch schema from the schema registry
1. [Envoys](./TestEnvoys/) - Client library and server stub library for various schema definitions
1. Counter [client](./CounterClient/) & [server](./CounterServer/) - Client and server for the counter definition
1. Additional [client](./SampleClient/) & [server](./SampleServer/) - Client and server for different DTDL definitions

### Connectors

1. [Event driven TCP connector](./EventDrivenTcpThermostatConnector/) - Event driven Connector from a simulated thermostat
1. [Polling REST connector](./PollingRestThermostatConnector/) - Polling Connecter from a REST endpoint
1. [Polling SQL connector](./SqlConnectorApp/) - Polling connector from a SQL endpoint

### Supporting

1. [TCP service app](./SampleTcpServiceApp/) - Simulated thermostat endpoint for the event driven Connector

## Build and run

1. Open a shell and navigate to the sample directory

1. Build the sample:

    ```bash
    dotnet build
    ```

1. Run the sample:

    ```bash
    dotnet run
    ```
 