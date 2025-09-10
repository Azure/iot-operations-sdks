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
        public required Func<IStreamContext<ReceivedStreamingExtendedRequest<TReq>>, RequestStreamMetadata, Func<Dictionary<string, string>>, CancellationToken, IAsyncEnumerable<StreamingExtendedResponse<TResp>>> OnStreamingCommandReceived { get; set; }

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

        /// <summary>
        /// If true, this executor will acknowledge the MQTT message associated with each streaming request as soon as it arrives.
        /// If false, the user must call <see cref="ReceivedStreamingExtendedRequest{TReq}.AcknowledgeAsync"/> once they are done processing
        /// each request message.
        /// </summary>
        /// <remarks>
        /// Generally, delaying acknowledgement allows for re-delivery by the broker in cases where the executor crashes or restarts unexpectedly.
        /// However, MQTT acknowledgements must be delivered in order, so delaying these acknowledgements may affect the flow of acknowledgements
        /// being sent by other processes using this same MQTT client. Additionally, the MQTT broker has a limit on the number of un-acknowledged messages
        /// that are allowed to be in-flight at a single moment, so delaying too many acknowledgements may halt all further MQTT traffic on the underlying
        /// MQTT client.
        /// </remarks>
        public bool AutomaticallyAcknowledgeRequests { get; set; } = true;

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
