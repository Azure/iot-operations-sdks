// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public abstract class StreamingCommandExecutor<TReq, TResp> : IAsyncDisposable
        where TReq : class
        where TResp : class
    {
        /// <summary>
        /// The timeout for all commands received by this executor.
        /// </summary>
        /// <remarks>
        /// Note that a command invoker may also send a per-invocation timeout. When this happens, a command will timeout if it exceeds either
        /// of these timeout values.
        /// </remarks>
        public TimeSpan ExecutionTimeout { get; set; }

        /// <summary>
        /// A streaming command was invoked
        /// </summary>
        /// <remarks>
        /// The callback provides the stream of requests and requires the user to return one to many responses.
        /// </remarks>
        public required Func<IStreamContext<StreamingExtendedRequest<TReq>>, RequestStreamMetadata, CancellationToken, IAsyncEnumerable<StreamingExtendedResponse<TResp>>> OnStreamingCommandReceived { get; set; }

        public string ServiceGroupId { get; init; }

        public string RequestTopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        /// <summary>
        /// The topic token replacement map that this executor will use by default. Generally, this will include the token values
        /// for topic tokens such as "executorId" which should be the same for the duration of this command executor's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified when starting the executor by specifying the additionalTopicToken map in <see cref="StartAsync(int?, Dictionary{string, string}?, CancellationToken)"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

        public Task StartAsync(int? preferredDispatchConcurrency = null, CancellationToken cancellationToken = default)
        {
            // TODO: derive the expected request topic (like command executor does)

            // TODO: subscribe to the shared subscription prefixed request topic

            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            // TODO: Unsubscribe from the request topic derived in StartAsync

            throw new NotImplementedException();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();

            GC.SuppressFinalize(this);
        }
    }
}
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
