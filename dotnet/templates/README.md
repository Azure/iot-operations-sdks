# Azure IoT Operations .NET Project Templates

This package contains the following project templates to help you develop Azure IoT Operations solutions:

 - Azure.Iot.Operations.Templates.PollingConnectorTemplate
    - A template for creating an AIO connector that polls an endpoint and forwards that polled telemetry to the AIO MQTT broker.
 - Azure.Iot.Operations.Templates.EventDrivenConnectorTemplate
    - A template for creating an AIO connector that listens for events from an endpoint and forwards those events the AIO MQTT broker as telemetry.


For examples on how to fill out these templates, see the completed samples [here](https://github.com/Azure/iot-operations-sdks/tree/main/dotnet/samples).

If you have any questions or need any support, please file an issue [here](https://github.com/Azure/iot-operations-sdks/issues)

## How To Install A .NET Project Template

To install these templates using the published package in Nuget.org, run the following command:

```bash
dotnet new install Azure.Iot.Operations.Templates
```

Alternatively, to install these templates from source, run the following command in this directory:

```bash
dotnet new install .
```

## How To Create A New Project With An Installed Project Template

You can use this locally installed project template when creating a new project in Visual Studio:

`File -> New Project -> Select the installed project template`

Alternatively, you can create a new project from an installed template from command line:

```bash
dotnet new aiopollingtelemetryconnector -n MyConnectorApp
```

Where "aiopollingtelemetryconnector" is the short name defined in the project template's [template.json file](./content/PollingConnectorTemplate/.template.config/template.json) and where "MyConnectorApp" is the name of your project.

Note that this command will create the project "MyConnectorApp" in the same directory that the command was run from.

## How To Uninstall A .NET Project Template

To uninstall these templates when they were installed from the published package in Nuget.org, run the following command:

```bash
dotnet new uninstall Azure.Iot.Operations.Templates
```

Alternatively, to uninstall these templates when they were installed from source, run the following command in this directory:

```bash
dotnet new uninstall .
```