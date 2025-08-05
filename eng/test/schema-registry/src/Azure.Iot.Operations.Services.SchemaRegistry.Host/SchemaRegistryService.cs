﻿
namespace Azure.Iot.Operations.Services.SchemaRegistry.Host;

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.SchemaRegistry.SchemaRegistry;
using Azure.Iot.Operations.Services.StateStore;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SchemaInfo = SchemaRegistry.Schema;


internal class SchemaRegistryService(ApplicationContext applicationContext, MqttSessionClient mqttClient, ILogger<SchemaRegistryService> logger, SchemaValidator schemaValidator)
    : SchemaRegistry.Service(applicationContext, mqttClient)
{
    readonly Utf8JsonSerializer _jsonSerializer = new();

    public override async Task<ExtendedResponse<GetResponsePayload>> GetAsync(GetRequestSchema request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        await using StateStoreClient _stateStoreClient = new(applicationContext, mqttClient);
        logger.LogInformation("Get request {req}", request.Name);
        StateStoreGetResponse resp = await _stateStoreClient.GetAsync(request.Name!, cancellationToken: cancellationToken);
        logger.LogInformation("Schema found {found}", resp.Value != null);
        SchemaInfo sdoc = null!;
        if (resp.Value != null)
        {
            sdoc = _jsonSerializer.FromBytes<SchemaInfo>(new(resp.Value?.Bytes), Utf8JsonSerializer.ContentType, Utf8JsonSerializer.PayloadFormatIndicator);
        }
        return new ExtendedResponse<GetResponsePayload>
        {
            Response = new()
            {
                Schema = sdoc
            }
        };
    }

    public override async Task<ExtendedResponse<PutResponsePayload>> PutAsync(PutRequestSchema request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        await using StateStoreClient _stateStoreClient = new(applicationContext, mqttClient);
        logger.LogInformation("RegisterSchema request");

        if (!schemaValidator.ValidateSchema(request.SchemaContent, request.Format.ToString()!))
        {
            throw new ApplicationException($"Invalid {request.Format} schema");
        }

        byte[] inputBytes = Encoding.UTF8.GetBytes(request.SchemaContent!);
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
                SchemaContent = request.SchemaContent,
                Format = request.Format,
                Version = "1.0.0",
                Tags = request.Tags,
                SchemaType = request.SchemaType,
                Namespace = "DefaultSRNamespace"
            };
            ReadOnlySequence<byte> schemaInfoBytes = _jsonSerializer.ToBytes(schemaInfo)!.SerializedPayload;
            StateStoreSetResponse resp = await _stateStoreClient.SetAsync(id, new StateStoreValue(schemaInfoBytes.ToArray()), new StateStoreSetRequestOptions() { }, cancellationToken: cancellationToken);
            logger.LogInformation("RegisterSchema response success: {s} {id}", resp.Success, id);
        }
        else
        {
            logger.LogInformation("Schema already exists {id}", id);
            schemaInfo = _jsonSerializer.FromBytes<SchemaInfo>(new(find.Value.Bytes), Utf8JsonSerializer.ContentType, Utf8JsonSerializer.PayloadFormatIndicator)!;
        }

        return new ExtendedResponse<PutResponsePayload>
        {
            Response = new()
            {
                Schema = schemaInfo
            }
        };
    }
}