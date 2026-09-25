// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry;

public interface IEdgeRegistryClient : ICoreClient, ISchemaRegistryClient, IThingDescriptionClient, IThingModelClient
{
    /// <summary>Makes this client unsubscribe from any broker topics it subscribed to, without disposing it.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once the client has stopped.</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
