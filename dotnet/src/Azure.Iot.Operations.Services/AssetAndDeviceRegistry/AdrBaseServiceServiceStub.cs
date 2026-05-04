// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Generated.AdrBaseService;

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

internal class AdrBaseServiceServiceStub(ApplicationContext applicationContext, IMqttPubSubClient mqttClient, Dictionary<string, string>? topicTokenMap = null)
    : AdrBaseService.Service(applicationContext, mqttClient, topicTokenMap), IAdrBaseServiceServiceStub
{
    public override Task<ExtendedResponse<CreateOrUpdateDiscoveredAssetResponsePayload>> CreateOrUpdateDiscoveredAssetAsync(CreateOrUpdateDiscoveredAssetRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public override Task<ExtendedResponse<GetAssetResponsePayload>> GetAssetAsync(GetAssetRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public override Task<ExtendedResponse<GetAssetStatusResponsePayload>> GetAssetStatusAsync(GetAssetStatusRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public override Task<ExtendedResponse<GetDeviceResponsePayload>> GetDeviceAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public override Task<ExtendedResponse<GetDeviceStatusResponsePayload>> GetDeviceStatusAsync(CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public override Task<ExtendedResponse<SetNotificationPreferenceForAssetUpdatesResponsePayload>> SetNotificationPreferenceForAssetUpdatesAsync(SetNotificationPreferenceForAssetUpdatesRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public override Task<ExtendedResponse<SetNotificationPreferenceForDeviceUpdatesResponsePayload>> SetNotificationPreferenceForDeviceUpdatesAsync(SetNotificationPreferenceForDeviceUpdatesRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public override Task<ExtendedResponse<UpdateAssetStatusResponsePayload>> UpdateAssetStatusAsync(UpdateAssetStatusRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public override Task<ExtendedResponse<UpdateDeviceStatusResponsePayload>> UpdateDeviceStatusAsync(UpdateDeviceStatusRequestPayload request, CommandRequestMetadata requestMetadata, CancellationToken cancellationToken)
    {
        // This class exists to access the telemetry senders associated with observability. These "not implemented" methods should
        // never be implemented (or called) since the ADR service handles these requests.
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync(bool disposing)
    {
        return base.DisposeAsync(disposing, CancellationToken.None);
    }

    public ValueTask DisposeAsync(CancellationToken cancellationToken)
    {
        return base.DisposeAsync(false, CancellationToken.None);
    }
}
