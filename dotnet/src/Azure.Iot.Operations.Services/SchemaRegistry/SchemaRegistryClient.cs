// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.SchemaRegistry;

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.SchemaRegistry.Generated;
using Azure.Iot.Operations.Services.SchemaRegistry.Models;

public class SchemaRegistryClient(ApplicationContext applicationContext, IMqttPubSubClient pubSubClient) : ISchemaRegistryClient
{
    private static readonly TimeSpan s_DefaultCommandTimeout = TimeSpan.FromSeconds(10);
    private readonly SchemaRegistryClientStub _clientStub = new(applicationContext, pubSubClient);
    private bool _disposed;

    /// <inheritdoc/>
    public async Task<SchemaRegistry.Schema> GetAsync(
        string schemaId,
        string version = "1",
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            return Converter.toModel((await _clientStub.GetAsync(
                new GetRequestSchema()
                {
                    Name = schemaId,
                    Version = version,
                }, null, null, timeout ?? s_DefaultCommandTimeout, cancellationToken)).Schema);
        }
        catch (Azure.Iot.Operations.Services.SchemaRegistry.Generated.SchemaRegistryErrorException srEx)
        {
            throw Converter.toModel(srEx);
        }
        catch (AkriMqttException ex) when (ex.Kind == AkriMqttErrorKind.PayloadInvalid)
        {
            // This is likely because the user received a "not found" response payload from the service, but the service is an
            // older version that sends an empty payload instead of the expected "{}" payload.
            throw new Models.SchemaRegistryErrorException(new()
            {
                Code = Models.SchemaRegistryErrorCode.NotFound,
            });
        }
        catch (AkriMqttException e) when (e.Kind == AkriMqttErrorKind.UnknownError)
        {
            // ADR 15 specifies that schema registry clients should still throw a distinct error when the service returns a 422. It also specifies
            // that the protocol layer should no longer recognize 422 as an expected error kind, so assume unknown errors are just 422's
            throw new Models.SchemaRegistryErrorException(new()
            {
                Code = Models.SchemaRegistryErrorCode.BadRequest,
                Details = new()
                {
                    Message = string.Format("Invocation error returned by schema registry service. Property name {0}, property value {1}", e.PropertyName, e.PropertyValue)
                }
            });
        }
    }

    /// <inheritdoc/>
    public async Task<SchemaRegistry.Schema> PutAsync(
        string schemaContent,
        SchemaRegistry.Format schemaFormat,
        SchemaRegistry.SchemaType schemaType = SchemaRegistry.SchemaType.MessageSchema,
        string version = "1",
        Dictionary<string, string>? tags = null,
        string? displayName = null,
        string? description = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        try
        { 
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            return Converter.toModel((await _clientStub.PutAsync(
                new PutRequestSchema()
                {
                    Format = Converter.fromModel(schemaFormat),
                    SchemaContent = schemaContent,
                    Version = version,
                    Tags = tags,
                    SchemaType = Converter.fromModel(schemaType),
                    Description = description,
                    DisplayName = displayName
                }, null, null, timeout ?? s_DefaultCommandTimeout, cancellationToken)).Schema);
        }
        catch (Azure.Iot.Operations.Services.SchemaRegistry.Generated.SchemaRegistryErrorException srEx)
        {
            throw Converter.toModel(srEx);
        }
        catch (AkriMqttException e) when (e.Kind == AkriMqttErrorKind.UnknownError)
        {
            // ADR 15 specifies that schema registry clients should still throw a distinct error when the service returns a 422. It also specifies
            // that the protocol layer should no longer recognize 422 as an expected error kind, so assume unknown errors are just 422's
            throw new Models.SchemaRegistryErrorException(new()
            {
                Code = Models.SchemaRegistryErrorCode.BadRequest,
                Details = new()
                {
                    Message = string.Format("Invocation error returned by schema registry service. Property name {0}, property value {1}", e.PropertyName, e.PropertyValue)
                }
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _clientStub.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
        _disposed = true;
    }
}
