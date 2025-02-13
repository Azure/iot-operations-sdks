// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Akri;

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Akri.DiscoveredAssetResources;

using AssetEndpointProfileResponseInfo = DiscoveredAssetResources.CreateDiscoveredAssetEndpointProfileResponseSchema;
using AssetResponseInfo = DiscoveredAssetResources.CreateDiscoveredAssetResponseSchema;

public class DiscoveredAssetResourcesClient : IDiscoveredAssetResourcesClient
{
    private readonly ApplicationContext _applicationContext;
    private readonly DiscoveredAssetResourcesClientStub _clientStub;
    private bool _disposed;

    public DiscoveredAssetResourcesClient(IMqttPubSubClient pubSubClient)
    {
        _applicationContext = new ApplicationContext(); // Create a new application context. TODO - have kept it here to pass current tests.
        _clientStub = new DiscoveredAssetResourcesClientStub(_applicationContext, pubSubClient); // Pass it to the stub
    }
    public async Task<AssetEndpointProfileResponseInfo?> CreateDiscoveredAssetEndpointProfileAsync(
        CreateDiscoveredAssetEndpointProfileRequestPayload discoveredAssetEndpointProfileCommandRequest, TimeSpan? timeout = default, CancellationToken cancellationToken = default!)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _clientStub.CreateDiscoveredAssetEndpointProfileAsync(
            discoveredAssetEndpointProfileCommandRequest, null, timeout, cancellationToken)).CreateDiscoveredAssetEndpointProfileResponse;
    }

    public async Task<AssetResponseInfo?> CreateDiscoveredAssetAsync(
        CreateDiscoveredAssetRequestPayload discoveredAssetCommandRequest, TimeSpan? timeout = default, CancellationToken cancellationToken = default!)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _clientStub.CreateDiscoveredAssetAsync(discoveredAssetCommandRequest, null, timeout, cancellationToken)).CreateDiscoveredAssetResponse;
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

    public async ValueTask DisposeAsync(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        await _clientStub.DisposeAsync(disposing).ConfigureAwait(false);
        if (disposing)
        {
            GC.SuppressFinalize(this);
        }

        _disposed = true;
    }

}

