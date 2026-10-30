// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using Azure.Iot.Operations.Protocol.RPC;

namespace Azure.Iot.Operations.Services.StateStore
{
    /// <summary>
    /// Provides caller-driven access to pages of State Store keys.
    /// </summary>
    public sealed class ListKeysPageIterator
    {
        private readonly IStateStoreClientStub _clientStub;
        private readonly byte[] _pattern;
        private readonly TimeSpan? _requestTimeout;
        private byte[]? _continuationToken;
        private bool _isComplete;
        private int _isRequestInProgress;

        internal ListKeysPageIterator(
            IStateStoreClientStub clientStub,
            byte[] pattern,
            TimeSpan? requestTimeout)
        {
            _clientStub = clientStub;
            _pattern = pattern.ToArray();
            _requestTimeout = requestTimeout;
        }

        /// <summary>
        /// Requests the next page of keys from the State Store.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// The next page of keys, which may be empty, or <see langword="null"/> when all pages
        /// have been returned.
        /// </returns>
        /// <remarks>
        /// Each call makes at most one request. If a request fails, the continuation token is
        /// unchanged and the call may be retried. Concurrent calls are not supported.
        /// </remarks>
        public async Task<IReadOnlyList<StateStoreKey>?> GetNextPageAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_isComplete)
            {
                return null;
            }

            if (Interlocked.CompareExchange(ref _isRequestInProgress, 1, 0) != 0)
            {
                throw new InvalidOperationException(
                    "A request for the next page is already in progress.");
            }

            try
            {
                byte[] requestPayload =
                    StateStorePayloadParser.BuildListKeysRequestPayload(
                        _pattern,
                        _continuationToken);
                Trace.TraceInformation($"LISTKEYS {Encoding.ASCII.GetString(_pattern)}");

                ExtendedResponse<byte[]> commandResponse =
                    await _clientStub.InvokeAsync(
                        requestPayload,
                        commandTimeout: _requestTimeout,
                        cancellationToken: cancellationToken).WithMetadata().ConfigureAwait(false);

                if (commandResponse.Response == null
                    || commandResponse.Response.Length == 0)
                {
                    throw new StateStoreOperationException(
                        "Received no response payload from State Store");
                }

                (List<byte[]> keys, byte[]? continuationToken) =
                    StateStorePayloadParser.ParseListKeysResponse(commandResponse.Response);

                _continuationToken = continuationToken;
                _isComplete = continuationToken == null;

                return keys.ConvertAll(key => new StateStoreKey(key));
            }
            finally
            {
                Interlocked.Exchange(ref _isRequestInProgress, 0);
            }
        }
    }
}
