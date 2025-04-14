// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Services.StateStore
{
    /// <summary>
    /// Internal interface that allows our unit tests to better mock calls to the generated state store client
    /// </summary>
    internal interface IStateStoreClientStub : IAsyncDisposable
    {
        RpcCallAsync<byte[]> InvokeAsync(byte[] request, CommandRequestMetadata? requestMetadata = null, Dictionary<string, string>? additionalTopicTokenMap = null, TimeSpan? commandTimeout = default, CancellationToken cancellationToken = default);
    }
}
