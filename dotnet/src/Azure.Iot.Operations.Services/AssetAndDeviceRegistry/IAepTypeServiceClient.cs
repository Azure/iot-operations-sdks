// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using  Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AepTypeService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public interface IAepTypeServiceClient : IAsyncDisposable
{
    Task<CreateDiscoveredAssetEndpointProfileResponseSchema?> CreateDiscoveredAssetEndpointProfileAsync(
        string additionalConfiguration,
        string daepName,
        string endpointProfileType,
        List<SupportedAuthenticationMethodsSchemaElementSchema> supportedAuthenticationMethods,
        string targetAddress,
        TimeSpan? commandTimeout = null,
        CancellationToken cancellationToken = default);
}
