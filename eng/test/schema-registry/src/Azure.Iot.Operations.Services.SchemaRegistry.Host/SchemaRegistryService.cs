
namespace Azure.Iot.Operations.Services.SchemaRegistry.Host;

using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using Azure.Iot.Operations.Services.StateStore;
using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.RPC;
using System.Security.Cryptography;
using System.Text;
using SchemaInfo = dtmi_ms_adr_SchemaRegistry__1.Object_Ms_Adr_SchemaRegistry_Schema__1;


internal class SchemaRegistryService(MqttSessionClient mqttClient, ILogger<SchemaRegistryService> logger, SchemaValidator schemaValidator) 
    : SchemaRegistry.Service(mqttClient)
{
    readonly Utf8JsonSerializer _jsonSerializer = new();
    
    public override async Task<ExtendedResponse<PutResponsePayload>> PutAsync(PutRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        await using StateStoreClient _stateStoreClient = new(mqttClient);
        logger.LogInformation("RegisterSchema request");

        if (!schemaValidator.ValidateSchema(request.PutSchemaRequest.SchemaContent, request.PutSchemaRequest.Format.ToString()!))
        {
            throw new ApplicationException($"Invalid {request.PutSchemaRequest.Format} schema");
        }

        byte[] inputBytes = Encoding.UTF8.GetBytes(request.PutSchemaRequest.SchemaContent!);
        byte[] inputHash = SHA256.HashData(inputBytes);
        string id = Convert.ToHexString(inputHash);

        logger.LogInformation("Trying to register schema {id}", id);
        SchemaInfo schemaInfo;

        StateStoreGetResponse find = await _stateStoreClient.GetAsync(id, cancellationToken: cancellationToken);
        if (find.Value == null)
        {
            schemaInfo = new()
            {
                Name = id,
                SchemaContent = request.PutSchemaRequest.SchemaContent,
                Format = request.PutSchemaRequest.Format,
                Version = "1.0.0",
                Tags = request.PutSchemaRequest.Tags,
                SchemaType = request.PutSchemaRequest.SchemaType,
                Namespace = "DefaultSRNamespace"
            };
            byte[] schemaInfoBytes = _jsonSerializer.ToBytes(schemaInfo)!.SerializedPayload;
            StateStoreSetResponse resp = await _stateStoreClient.SetAsync(id, new StateStoreValue(schemaInfoBytes), new StateStoreSetRequestOptions() { }, cancellationToken: cancellationToken);
            logger.LogInformation("RegisterSchema response success: {s} {id}", resp.Success, id);
        }
        else
        {
            logger.LogInformation("Schema already exists {id}", id);
            schemaInfo = _jsonSerializer.FromBytes<SchemaInfo>(find.Value.Bytes, Utf8JsonSerializer.ContentType, Utf8JsonSerializer.PayloadFormatIndicator)!;
        }

        return new ExtendedResponse<PutResponsePayload>
        {
            Response = new()
            {
                Schema = schemaInfo
            }
        };
    }

    public override async Task<ExtendedResponse<GetResponsePayload>> GetAsync(GetRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        await using StateStoreClient _stateStoreClient = new(mqttClient);
        logger.LogInformation("Get request {req}", request.GetSchemaRequest.Name);
        StateStoreGetResponse resp = await _stateStoreClient.GetAsync(request.GetSchemaRequest.Name!, cancellationToken: cancellationToken);
        logger.LogInformation("Schema found {found}", resp.Value != null);
        SchemaInfo sdoc = null!;
        if (resp.Value != null)
        {
            sdoc = _jsonSerializer.FromBytes<SchemaInfo>(resp.Value?.Bytes, Utf8JsonSerializer.ContentType, Utf8JsonSerializer.PayloadFormatIndicator);
        }
        return new ExtendedResponse<GetResponsePayload>
        {
            Response = new()
            {
                Schema = sdoc
            }
        };
    }
}
