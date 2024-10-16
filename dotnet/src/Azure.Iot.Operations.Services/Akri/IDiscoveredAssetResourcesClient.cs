namespace Azure.Iot.Operations.Services.Akri;

using AssetEndpointProfileResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAssetEndpointProfile_Response;
using AssetEndpointProfileRequestAuthMethodSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Enum_CreateDiscoveredAssetEndpointProfile_Request_SupportedAuthenticationMethods_ElementSchema;
using AssetResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Response;
using AssetRequestDatasetsElementSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_Datasets_ElementSchema;
using AssetRequestDefaultTopic = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_DefaultTopic;
using AssetRequestEventsSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_Events_ElementSchema;
public interface IDiscoveredAssetResourcesClient : IAsyncDisposable
{
    public Task<AssetEndpointProfileResponseInfo?> CreateDiscoveredAssetEndpointProfileAsync(
        string additionalConfiguration,
        string daepName,
        string endpointProfileType,
        List<AssetEndpointProfileRequestAuthMethodSchema> assetEndpointProfileRequestAuthMethodSchemas,
        string targetAddress,
        TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
    public Task<AssetResponseInfo?> CreateDiscoveredAssetAsync(
        string assetEndpointProfileRef, string assetName,
        List<AssetRequestDatasetsElementSchema> datasets, string defaultDatasetsConfiguration,
        string defaultEventsConfiguration, AssetRequestDefaultTopic defaultTopic,
        string documentationUri, List<AssetRequestEventsSchema> eventsSchema, string hardwareRevision,
        string manufacturer, string manufacturerUri, string productCode,
        string model, string serialNumber, string softwareRevision,
        TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
}

