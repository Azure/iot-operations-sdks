# DssCLI

Command line tool (based on .NET tool) to interact with the IoT Mqtt Broker State Store.

## Install

> Requires .NET8

```bash
dotnet tool install Azure.Iot.Operations.Services.DssCli --prerelease --global --add-source https://pkgs.dev.azure.com/e4k-sdk/SdkLite/_packaging/sdklite-previews/nuget/v3/index.json
```

## Usage

```
dsscli 
--get <key> 
--set <key> -value <value> | --file <file>
--del <key>

[optional settings]
    --mqttDiag false
    --ConnectionStrings:Default 'Hostname=localhost;TcpPort=1883;UseTls=false;'
    --Logging:LogLevel:Default=Information
```

## Connection String

If the connection string is not specified, it will default to `Hostname=localhost;TcpPort=1883;UseTls=false;`, and stored in a local file. If set, the local file will be updated with the new connection string.
