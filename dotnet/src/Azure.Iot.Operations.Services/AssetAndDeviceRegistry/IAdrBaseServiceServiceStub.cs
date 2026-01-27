// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using static Azure.Iot.Operations.Services.AssetAndDeviceRegistry.AdrBaseService.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry
{
    // An interface for the code gen'd AdrBaseServiceServiceStub so that we can mock it in our unit tests
    internal interface IAdrBaseServiceServiceStub : IAsyncDisposable
    {
        DeviceEndpointRuntimeHealthEventTelemetrySender DeviceEndpointRuntimeHealthEventTelemetrySender { get; }

        DatasetRuntimeHealthEventTelemetrySender DatasetRuntimeHealthEventTelemetrySender { get; }

        EventRuntimeHealthEventTelemetrySender EventRuntimeHealthEventTelemetrySender { get; }

        ManagementActionRuntimeHealthEventTelemetrySender ManagementActionRuntimeHealthEventTelemetrySender { get; }

        StreamRuntimeHealthEventTelemetrySender StreamRuntimeHealthEventTelemetrySender { get; }
    }
}
