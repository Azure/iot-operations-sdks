// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;

using SchemaInfo = Azure.Iot.Operations.Services.SchemaRegistry.Generated.Schema;
using SchemaFormat = Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry.Format;
using System.Diagnostics;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.SchemaRegistry.Models;

string jsonSchema1 = /*lang=json,strict*/ """
    {
        "$schema": "http://json-schema.org/draft-07/schema#",
        "type": "object",
        "properties": {
      	  "humidity": {
        	    "type": "integer"
        	},
        	"temperature": {
            	"type": "number"
        	}
        }
    }
    """;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();


var mqttDiag = Convert.ToBoolean(configuration["mqttDiag"]);
if (mqttDiag) Trace.Listeners.Add(new ConsoleTraceListener());
MqttSessionClient mqttClient = new(new MqttSessionClientOptions { EnableMqttLogging = mqttDiag });
ApplicationContext applicationContext = new();
await using SchemaRegistryClient schemaRegistryClient = new(applicationContext, mqttClient);
await mqttClient.ConnectAsync(MqttConnectionSettings.FromEnvVars());

SchemaInfo? schemaInfo = await schemaRegistryClient.PutAsync(jsonSchema1, SchemaFormat.JsonSchemaDraft07);
SchemaInfo? resolvedSchema = await schemaRegistryClient.GetAsync(schemaInfo?.Name!);

if (resolvedSchema == null)
{
    Console.WriteLine("Schema not found");
    return;
}

Console.WriteLine(resolvedSchema.Name);
Console.WriteLine(resolvedSchema.SchemaContent);

try
{
    await schemaRegistryClient.GetAsync("not found");
}
catch (SchemaRegistryErrorException e) when (e.SchemaRegistryError.Code == SchemaRegistryErrorCode.NotFound)
{
    Console.WriteLine("Schema was not found");
}
