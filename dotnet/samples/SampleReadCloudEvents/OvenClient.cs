namespace SampleReadCloudEvents;

using Azure.Iot.Operations.Services.SchemaRegistry;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Telemetry;
using dtmi_akri_samples_oven__1;


public class OvenClient(MqttSessionClient mqttClient, SchemaRegistryClient schemaRegistryClient, ILogger<OvenClient> logger) 
    :Oven.Client(mqttClient)
{

    Dictionary<string, string> schemaCache = new();

    public override async Task ReceiveTelemetry(string senderId, TelemetryCollection telemetry, IncomingTelemetryMetadata metadata)
    {
        logger.LogInformation("Received telemetry from {senderId} \n", senderId);
        if (metadata.CloudEvent != null)
        {
            logger.LogInformation("CloudEvents: \n" +
                "id: {id} \n " +
                "time: {time} \n " +
                "type: {type}\n " +
                "source: {source} \n " +
                "contenttype: {ct} \n " +
                "dataschema: {ds}", 
                metadata.CloudEvent.Id, 
                metadata.CloudEvent.Time,
                metadata.CloudEvent.Type, 
                metadata.CloudEvent.Source, 
                metadata.CloudEvent.DataContentType,
                metadata.CloudEvent.DataSchema);

            if (schemaCache.ContainsKey(metadata.CloudEvent.DataSchema!))
            {
                logger.LogInformation("Schema already cached");
            }
            else
            {
                logger.LogInformation("Schema not cached, fetching from SR");
                Uri schemaUri = new(metadata.CloudEvent.DataSchema!);
                var schemaInfo = await schemaRegistryClient.GetAsync(schemaUri.Segments[1]);
                schemaCache.Add(metadata.CloudEvent.DataSchema!, schemaInfo!.SchemaContent!);
                logger.LogInformation("Schema cached");
            }   

        }
    }
}
