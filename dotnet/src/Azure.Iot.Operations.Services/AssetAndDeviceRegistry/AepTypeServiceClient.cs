// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AepTypeServiceClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient) : IAepTypeServiceClient
{
    private readonly AepTypeServiceClientStub _client = new(applicationContext, mqttClient);
    private bool _disposed;
    private static readonly TimeSpan _defaultCommandTimeout = TimeSpan.FromSeconds(10);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _client.DisposeAsync().ConfigureAwait(false);;
        GC.SuppressFinalize(this);
        _disposed = true;
    }

    public async Task<CreateDiscoveredAssetEndpointProfileResponseSchema?> CreateDiscoveredAssetEndpointProfileAsync(
        string additionalConfiguration,
        string daepName,
        string endpointProfileType,
        List<SupportedAuthenticationMethodsSchemaElementSchema> supportedAuthenticationMethods,
        string targetAddress,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);

            return (await _client.CreateDiscoveredAssetEndpointProfileAsync(
                new CreateDiscoveredAssetEndpointProfileRequestPayload()
                {
                    DiscoveredAssetEndpointProfile = new()
                    {
                        AdditionalConfiguration = additionalConfiguration,
                        DaepName = daepName,
                        EndpointProfileType = endpointProfileType,
                        SupportedAuthenticationMethods = supportedAuthenticationMethods,
                        TargetAddress = targetAddress
                    }
                },
                null,
                null,
                commandTimeout ?? _defaultCommandTimeout,
                cancellationToken)).CreateDiscoveredAssetEndpointProfileResponse;
        }
        catch (AkriMqttException ex) when (ex.Kind == AkriMqttErrorKind.PayloadInvalid)
        {
            // This is likely because the user received a "not found" response payload from the service, but the service is an
            // older version that sends an empty payload instead of the expected "{}" payload.
            return null;
        }
        catch (AkriMqttException e) when (e.Kind == AkriMqttErrorKind.UnknownError)
        {
            // ADR 15 specifies that schema registry clients should still throw a distinct error when the service returns a 422. It also specifies
            // that the protocol layer should no longer recognize 422 as an expected error kind, so assume unknown errors are just 422's
            throw new AepTypeServiceException("Invocation error returned by AEP type service", e.PropertyName, e.PropertyValue);
        }
    }
}
