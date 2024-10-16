

namespace Azure.Iot.Operations.Services.Akri;

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1;

using AssetEndpointProfileResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAssetEndpointProfile_Response;
using AssetEndpointProfileRequestAuthMethodSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Enum_CreateDiscoveredAssetEndpointProfile_Request_SupportedAuthenticationMethods_ElementSchema;
using AssetResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Response;
using AssetRequestDatasetsElementSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_Datasets_ElementSchema;
using AssetRequestDefaultTopic = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_DefaultTopic;
using AssetRequestEventsSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_Events_ElementSchema;


public class DiscoveredAssetResourcesClient(IMqttPubSubClient pubSubClient) : IDiscoveredAssetResourcesClient
{
    private static readonly TimeSpan s_DefaultCommandTimeout = TimeSpan.FromSeconds(10);
    private readonly DiscoveredAssetResourcesClientStub _clientStub = new(pubSubClient);
    private bool _disposed;

    public async Task<AssetEndpointProfileResponseInfo?> CreateDiscoveredAssetEndpointProfileAsync(
        string additionalConfiguration,
        string daepName,
        string endpointProfileType,
        List<AssetEndpointProfileRequestAuthMethodSchema> assetEndpointProfileRequestAuthMethodSchemas,
        string targetAddress,
        TimeSpan? timeout = default,
        CancellationToken cancellationToken = default!)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _clientStub.CreateDiscoveredAssetEndpointProfileAsync(
            new CreateDiscoveredAssetEndpointProfileCommandRequest()
            {
                CreateDiscoveredAssetEndpointProfileRequest = new()
                {
                    AdditionalConfiguration = additionalConfiguration,
                    DaepName = daepName,
                    EndpointProfileType = endpointProfileType,
                    SupportedAuthenticationMethods = assetEndpointProfileRequestAuthMethodSchemas,
                    TargetAddress = targetAddress,
                }
            }, null, timeout ?? s_DefaultCommandTimeout, cancellationToken)).CreateDiscoveredAssetEndpointProfileResponse;
    }

    public async Task<AssetResponseInfo?> CreateDiscoveredAssetAsync(
        string assetEndpointProfileRef, string assetName, 
        List<AssetRequestDatasetsElementSchema> datasets, string defaultDatasetsConfiguration,
        string defaultEventsConfiguration, AssetRequestDefaultTopic defaultTopic,
        string documentationUri, List<AssetRequestEventsSchema> eventsSchema, string hardwareRevision,
        string manufacturer, string manufacturerUri, string productCode,
        string model, string serialNumber, string softwareRevision,
        TimeSpan? timeout = default,
        CancellationToken cancellationToken = default!)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _clientStub.CreateDiscoveredAssetAsync(
            new CreateDiscoveredAssetCommandRequest()
            {
                CreateDiscoveredAssetRequest = new()
                {
                    AssetEndpointProfileRef = assetEndpointProfileRef,
                    AssetName = assetName,
                    Datasets = datasets,
                    DefaultDatasetsConfiguration = defaultDatasetsConfiguration,
                    DefaultEventsConfiguration = defaultEventsConfiguration,
                    DefaultTopic = defaultTopic,
                    DocumentationUri = documentationUri,
                    Events = eventsSchema,
                    HardwareRevision = hardwareRevision,
                    Manufacturer = manufacturer,
                    ManufacturerUri = manufacturerUri,
                    Model = model,
                    ProductCode = productCode,
                    SerialNumber = serialNumber,
                    SoftwareRevision = softwareRevision,
                }
            }, null, timeout ?? s_DefaultCommandTimeout, cancellationToken)).CreateDiscoveredAssetResponse;
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

