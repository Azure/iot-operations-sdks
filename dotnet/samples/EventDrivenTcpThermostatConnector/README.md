# Event Driven TCP Thermostat Connector

This sample demonstrates a connector that reacts to events sent to it by an asset. In this case, the asset is a TCP service that will randomly send thermostat data on a TCP connection that is established once the asset is discovered.

## Setup your environment

1. Follow the [setup](/doc/setup.md) steps to configure your environment and the Azure IoT Operations cluster for development.

2. Deploy the Akri operator *(currently in private preview)*

## Creating the sample

This project was generated  from the [event driven telemetry connector](/dotnet/templates/EventDrivenTelemetryConnector/) template. 

For instructions on how to install the project template, see [the installation instructions](/dotnet/templates/README.md).

## Run the sample

1. Run the following [script](./deploy-connector-and-asset.sh) to deploy the connector and the simulated thermostat client to the cluster:

    ```bash
    ./deploy-connector-and-asset.sh
    ```

1. Subscribe to the following MQTT topic to observe the connector output:

    ```bash
    mosquitto_sub -L mqtt://localhost//mqtt/machine/status/change_event
    ```

    output:
    ```bash
    {"desiredTemperature":58.5972536781413,"currentTemperature":66.16511010453911}
    {"desiredTemperature":58.823127942960376,"currentTemperature":76.61413001297211}
    ```
