// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public readonly struct RpcCallAsync<TResp>(Task<ExtendedResponse<TResp>> task, Guid requestCorrelationData)
        where TResp : class
    {
        public Task<ExtendedResponse<TResp>> WithMetadata()
        {
            return ExtendedAsync;
        }

        public Task<ExtendedResponse<TResp>> ExtendedAsync { get; } = task;

        public Guid RequestCorrelationData { get; } = requestCorrelationData;

        public TaskAwaiter<TResp> GetAwaiter()
        {
            return GetResponseAsync().GetAwaiter();
        }

        private async Task<TResp> GetResponseAsync()
        {
            ExtendedResponse<TResp> extendedResponse = await ExtendedAsync.ConfigureAwait(false);
            return extendedResponse.Response;
        }
    }
}
