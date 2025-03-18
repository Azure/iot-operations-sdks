// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ReadCloudEventsSample;

using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using Azure.Iot.Operations.Protocol;
using ReadCloudEventsSample.Oven;

public class OvenClient(ApplicationContext applicationContext, MqttSessionClient mqttClient, SchemaRegistryClient schemaRegistryClient, ILogger<OvenClient> logger) 
    : Oven.Oven.Client(applicationContext, mqttClient)
{

    private readonly Dictionary<string, string> _schemaCache = new();

    public override async Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation("Received telemetry from {senderId} \n", senderId);
        CloudEvent? cloudEvent = null;
        try
        {
            cloudEvent = metadata.GetCloudEvent();
        }
        catch (ArgumentException e)
        {
            logger.LogError(e, "Failed to parse cloud event");
            return;
        }

        logger.LogInformation("CloudEvents: \n" +
            "id: {id} \n " +
            "time: {time} \n " +
            "type: {type}\n " +
            "source: {source} \n " +
            "contenttype: {ct} \n " +
            "dataschema: {ds}",
            cloudEvent.Id,
            cloudEvent.Time,
            cloudEvent.Type,
            cloudEvent.Source,
            cloudEvent.DataContentType,
            cloudEvent.DataSchema);

        if (_schemaCache.ContainsKey(cloudEvent.DataSchema!))
        {
            logger.LogInformation("Schema already cached");
        }
        else
        {
            logger.LogInformation("Schema not cached, fetching from SR");
            Uri schemaUri = new(cloudEvent.DataSchema!);
            var schemaInfo = await schemaRegistryClient.GetAsync(schemaUri.Segments[1]);
            _schemaCache.Add(cloudEvent.DataSchema!, schemaInfo!.SchemaContent!);
            logger.LogInformation("Schema cached");
        }   
    }
}
