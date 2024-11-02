# Develop an Akri Connector

## Setup

Setup your development environment for building an Akri Connector.

1. Follow the [Setup instruction](/doc/setup.md) to prepare your cluster for developing for Azure IoT Operations.
1. Install the [Akri Operator](setup.md).
1. Create the [Akri Connector configuration](configure.md) for configuration of the MQTT broker, the asset endpoint and the assets.

## Building

Build your Akri connector by using an existing template, or creating a custom application. For more information about Akri Connector development paths, refer to the [Akri Connector Overview](README.md#development-paths).

### Templates

The following templates are available:

* [.NET Connector Template](/dotnet/samples/GenericConnectorWorkerService)

Template samples:

* [.NET HTTP Thermostat Connector](/dotnet/samples/HttpThermostatConnectorApp)

### Custom

[TODO] how to link to language docs?

Refer to the [Application flow](flow.md) for details on building your own Akri Connector.

## Testing locally

Debugging the application locally on your machine simplifies development by aligning with the regular development environment.

1. Configure the application to run locally
1. Build and run the application
1. Observe code paths
1. Monitor the MQTT topics for each asset
1. View the schema registry for new schemas
1. Review Akri agent logs for new assets
1. Interact with the asset (command and control) by sending Mqtt message to the broker

## Testing on cluster

Once the Akri Connector is working as expected running on the local machine, the next step is to containerize the application and deploy it to the cluster.

1. Define the Dockerfile
1. Build the container
1. Push the container to the cluster
1. Define the deployment yaml
1. Deploy your container using kubectl

## Push to production

[TODO] TBD based on Akri Operator docs?
