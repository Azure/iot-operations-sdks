# Azure IoT Operation .NET Project Templates

This folder contains .NET project tempate definitions that are useful when developing
applications for your Azure IoT Operations environment.

## How To Install A .NET Project Template

To install a project template, simply navigate to the directory of the tempate you want to install and run the command:

```
dotnet new install .\
```

## How To Create A New Project With An Installed Project Template

You can use this locally installed project template when creating a new project in Visual Studio.

File -> New Project -> Select the installed project tempate

Alternatively, you can create a new project from an installed template from command line:

```
dotnet new aioconnectorapp -n MyConnectorApp
```

Where "aioconnectorapp" is the short name defined in the project template's [template.josn file](./ConnectorApp/.template.config/template.json) and where "MyConnectorApp" is the name of your project.


## How To Uninstall A .NET Project Template

Navigate to the directory with the project template to uninstall and run the command:

```
dotnet new uninstall .\
```