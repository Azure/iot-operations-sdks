﻿using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Protocol.RPC;
using System.Diagnostics;

namespace Azure.Iot.Operations.Services.StateStore
{
    // This wrapper class allows unit tests to mock the RPC layer since the RPC
    // layer doesn't expose an interface that allows for mocking
    internal class StateStoreGeneratedClientHolder : IStateStoreGeneratedClientHolder
    {
        dtmi_ms_aio_mq_StateStore__1.StateStore.Client? _generatedClient;

        internal StateStoreGeneratedClientHolder(dtmi_ms_aio_mq_StateStore__1.StateStore.Client generatedClient)
        { 
            _generatedClient = generatedClient;
        }

        internal StateStoreGeneratedClientHolder()
        {
            _generatedClient = null;
        }

        public virtual ValueTask DisposeAsync()
        {
            Debug.Assert(_generatedClient != null);
            return _generatedClient.DisposeAsync();
        }

        public virtual RpcCallAsync<byte[]> InvokeAsync(byte[] request, CommandRequestMetadata? requestMetadata = null, TimeSpan? commandTimeout = null, CancellationToken cancellationToken = default)
        {
            Debug.Assert(_generatedClient != null);
            return _generatedClient.InvokeAsync(request, requestMetadata, commandTimeout, cancellationToken);
        }
    }
}
