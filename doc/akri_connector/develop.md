# Develop an Akri Connector

## Setup

Setup your development environment for building an Akri Connector.

1. Follow the [Setup instruction](/doc/setup.md) to prepare your cluster for developing for Azure IoT Operations.
1. Deploy the [Akri Components](setup.md)
1. Create the [Akri Connector configuration](configure.md) to define the Assets and Asset Endpoints.

## Building

Build your Akri connector by using an existing template, or creating a custom application. For more information about Akri Connector development paths, refer to the [Akri Connector Overview](README.md#development-paths).

### Template Connector

The following templates are available:

* [.NET Connector Template](/dotnet/samples/GenericConnectorWorkerService)

Template samples:

* [.NET HTTP Thermostat Connector](/dotnet/samples/HttpThermostatConnectorApp)

### Custom Connector

> [!WARNING]
> Customer Akri Connector development assets are still being developed

## On-cluster development

To test the Akri Connector, you will need to create a container image and upload to your development cluster.

1. Define a Dockerfile
1. Build the container
1. Push the container to the cluster
1. Define the deployment yaml
1. Deploy your container using `kubectl`

## Local development

> [!NOTE]
> Local development (ability to launch the application directly from your development environment) is still under development.
