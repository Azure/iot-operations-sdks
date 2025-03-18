# .NET Samples

> [!CAUTION]
>
> The samples and tutorials provided in this repository are for **demonstration purposed only**, and should not be deployed to a production system or included within an application without a full understanding of how they operate.
>
> Additionally some samples may use a username and password for authentication. This is used for sample simplicity and a production system should use a robust authentication mechanism such as certificates.
>
> To learn more about securing your edge solution, refer to [Security best practices for IoT solutions](https://learn.microsoft.com/azure/iot/iot-overview-security).

This directory contains a variety of samples demonstrating how to use the .NET packages to interact with Azure IoT Operations.

## Run a sample

> [!TIP]
> Update the `.env` file in the repository root directory to change the authentication method with MQTT broker.

1. Follow the [setup](/doc/setup.md) directions to prepare an Azure IoT Operations cluster for development.

1. Open a shell and navigate to the sample directory

1. Build the sample:

    ```bash
    dotnet build
    ```

1. Run the sample using the default [environment](/.env):

    ```bash
    source `git rev-parse --show-toplevel`/.env; dotnet run
    ```

## Samples

### MQTT connection

1. [Session Client](./Mqtt/SessionClient/) - Create an MQTT session using the Session Client

### Services

1. [State store](./Services/StateStoreClient/) - Set, get and delete a key
1. [State store observe](./Services/StateStoreObserveKey/) - Be notified of changes to an observed key
1. [Leased lock](./Services/LeasedLockClient/) - Lock a key in the state store shared between applications
1. [Passive replication](./Services/PassiveReplication/) - Use the leader election client to perform passive replication
1. [Schema registry](./Services/SchemaRegistryClient/) - Put and get schemas in the schema registry

### Telemetry and RPC

1. [Cloud events](./Protocol/CloudEvents/) - Send telemetry with cloud events referencing the payload schema
1. [Read cloud events](./Protocol/ReadCloudEvents/) - Receive telemetry with cloud events and fetch schema from the schema registry
1. [Envoys](./Protocol/TestEnvoys/) - Client library and server stub library for various schema definitions
1. Counter [client / server](./Protocol/Counter) - Client and server for the counter definition
1. Additional [clients / servers](./Protocol/Codegen/) - Client and server for different DTDL definitions

### Connectors

1. [Event driven TCP connector](./Connectors/EventDrivenTcpThermostatConnector/) - Event driven Connector from a simulated thermostat
1. [Polling REST connector](./Connectors/PollingRestThermostatConnector/) - Polling Connecter from a REST endpoint
1. [Polling SQL connector](./Connectors/SqlConnector/) - Polling connector from a SQL endpoint
