

namespace Azure.Iot.Operations.Services.Akri;

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1;

using AssetEndpointProfileResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAssetEndpointProfile_Response;
using AssetResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Response;

public class DiscoveredAssetResourcesClient(IMqttPubSubClient pubSubClient) : IDiscoveredAssetResourcesClient
{
    private readonly DiscoveredAssetResourcesClientStub _clientStub = new(pubSubClient);
    private bool _disposed;

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

