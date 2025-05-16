// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.Observability;

using Protocol.RPC;
using AkriObservabilityService;

public interface IAkriObservabilityService
{
    RpcCallAsync<PublishMetricsResponsePayload> PublishMetricsAsync(PublishMetricsRequestPayload request);
}
