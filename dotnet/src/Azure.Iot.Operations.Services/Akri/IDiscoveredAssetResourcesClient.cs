﻿//namespace Azure.Iot.Operations.Services.Akri;

//using AssetEndpointProfileResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAssetEndpointProfile_Response;
//using AssetEndpointProfileRequestAuthMethodSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Enum_CreateDiscoveredAssetEndpointProfile_Request_SupportedAuthenticationMethods_ElementSchema;
//using AssetResponseInfo = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Response;
//using AssetRequestDatasetsElementSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_Datasets_ElementSchema;
//using AssetRequestDefaultTopic = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_DefaultTopic;
//using AssetRequestEventsSchema = dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1.Object_CreateDiscoveredAsset_Request_Events_ElementSchema;
//using Azure.Iot.Operations.Services.Akri.dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1;

//public interface IDiscoveredAssetResourcesClient : IAsyncDisposable
//{
//    public Task<AssetEndpointProfileResponseInfo?> CreateDiscoveredAssetEndpointProfileAsync(
//        CreateDiscoveredAssetEndpointProfileRequestPayload discoveredAssetEndpointProfileCommandRequest,
//        TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
//    public Task<AssetResponseInfo?> CreateDiscoveredAssetAsync(
//        CreateDiscoveredAssetRequestPayload discoveredAssetCommandRequest,
//        TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
//}

