// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Services.StateStore
{
    // This interface allows unit tests to mock the RPC layer since the RPC
    // layer doesn't expose an interface that allows for mocking
    internal interface IStateStoreGeneratedClientHolder : IAsyncDisposable
    {
        RpcCallAsync<byte[]> InvokeAsync(byte[] request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);
    }
}