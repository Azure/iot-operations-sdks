﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Protocol.Connection;
using Azure.Iot.Operations.Mqtt.Session;

using SchemaInfo = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Object_Ms_Adr_SchemaRegistry_Schema__1;
using SchemaFormat = Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_Format__1;
using System.Diagnostics;

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
await using SchemaRegistryClient schemaRegistryClient = new(mqttClient);
await mqttClient.ConnectAsync(MqttConnectionSettings.FromConnectionString(configuration.GetConnectionString("Default")!));

SchemaInfo? schemaInfo = await schemaRegistryClient.PutAsync(jsonSchema1, SchemaFormat.JsonSchemaDraft07);
// "9045385BAD270EE5840D1F88F202B21444920F7A5486B8B69ED86DDC0A30E936"
SchemaInfo? resolvedSchema = await schemaRegistryClient.GetAsync(schemaInfo?.Name!);

if (resolvedSchema == null)
{
    Console.WriteLine("Schema not found");
    return;
}

Console.WriteLine(resolvedSchema.Name);
Console.WriteLine(resolvedSchema.SchemaContent);


SchemaInfo? notfound = await schemaRegistryClient.GetAsync("not found");
Console.WriteLine(notfound == null ? "notfound" : "found");