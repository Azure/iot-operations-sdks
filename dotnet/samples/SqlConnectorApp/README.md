# SQL Connector

This sample demonstrates a connector thats polls a SQL database endpoint for asset information.

## Setup your environment

1. Follow the [setup](/doc/setup.md) steps to configure your environment and the Azure IoT Operations cluster for development.

2. Deploy the Akri operator *(currently in private preview)*

## Creating the sample

This project was generated from the [polling telemetry connector](/dotnet/templates/PollingTelemetryConnector/) template. 

For instructions on how to install the project template, see [the installation instructions](/dotnet/templates/README.md).

## Run the sample

1. Run the following [script](./deploy-connector-and-asset.sh) to deploy the connector and the simulated thermostat client to the cluster:

    ```bash
    ./deploy-connector-and-asset.sh
    ```

1. Subscribe to the following MQTT topic to observe the connector output:

    ```bash
    mosquitto_sub -L mqtt://localhost//mqtt/machine/data
    ```

    output:
    ```bash
    [{"country":"us","viscosity":0.5,"sweetness":0.8,"particleSize":0.7,"overall":0.4},{"country":"fr","viscosity":0.6,"sweetness":0.85,"particleSize":0.75,"overall":0.45},{"country":"jp","viscosity":0.53,"sweetness":0.83,"particleSize":0.73,"overall":0.43},{"country":"uk","viscosity":0.51,"sweetness":0.81,"particleSize":0.71,"overall":0.41}]
    [{"country":"us","viscosity":0.5,"sweetness":0.8,"particleSize":0.7,"overall":0.4},{"country":"fr","viscosity":0.6,"sweetness":0.85,"particleSize":0.75,"overall":0.45},{"country":"jp","viscosity":0.53,"sweetness":0.83,"particleSize":0.73,"overall":0.43},{"country":"uk","viscosity":0.51,"sweetness":0.81,"particleSize":0.71,"overall":0.41}]
    ```
