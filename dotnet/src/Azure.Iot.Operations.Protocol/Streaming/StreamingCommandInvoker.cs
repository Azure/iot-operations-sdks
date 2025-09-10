// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS0168 // Variable is declared but never used

namespace Azure.Iot.Operations.Protocol.Streaming
{
    public abstract class StreamingCommandInvoker<TReq, TResp> : IAsyncDisposable
        where TReq : class
        where TResp : class
    {
        /// <summary>
        /// The topic token replacement map that this streaming command invoker will use by default. Generally, this will include the token values
        /// for topic tokens such as "modelId" which should be the same for the duration of this command invoker's lifetime.
        /// </summary>
        /// <remarks>
        /// Tokens replacement values can also be specified per-method invocation by specifying the additionalTopicToken map in <see cref="InvokeCommandAsync(TReq, CommandRequestMetadata?, Dictionary{string, string}?, TimeSpan?, CancellationToken)"/>.
        /// </remarks>
        public Dictionary<string, string> TopicTokenMap { get; protected set; }

        public string RequestTopicPattern { get; init; }

        public string? TopicNamespace { get; set; }

        /// <summary>
        /// The prefix to use in the command response topic. This value is ignored if <see cref="ResponseTopicPattern"/> is set.
        /// </summary>
        /// <remarks>
        /// If no prefix or suffix is specified, and no value is provided in <see cref="ResponseTopicPattern"/>, then this
        /// value will default to "clients/{invokerClientId}" for security purposes.
        /// 
        /// If a prefix and/or suffix are provided, then the response topic will use the format:
        /// {prefix}/{command request topic}/{suffix}.
        /// </remarks>
        public string? ResponseTopicPrefix { get; set; }

        /// <summary>
        /// The suffix to use in the command response topic. This value is ignored if <see cref="ResponseTopicPattern"/> is set.
        /// </summary>
        /// <remarks>
        /// If no suffix is specified, then the command response topic won't include a suffix.
        /// 
        /// If a prefix and/or suffix are provided, then the response topic will use the format:
        /// {prefix}/{command request topic}/{suffix}.
        /// </remarks>
        public string? ResponseTopicSuffix { get; set; }

        /// <summary>
        /// If provided, this topic pattern will be used for command response topic.
        /// </summary>
        /// <remarks>
        /// If not provided, and no value is provided for <see cref="ResponseTopicPrefix"/> or <see cref="ResponseTopicSuffix"/>, the default pattern used will be clients/{mqtt client id}/{request topic pattern}.
        /// </remarks>
        public string? ResponseTopicPattern { get; set; }

        /// <summary>
        /// If true, this invoker will acknowledge the MQTT message associated with each streaming response as soon as it arrives.
        /// If false, the user must call <see cref="ReceivedStreamingExtendedResponse{TResp}.AcknowledgeAsync"/> once they are done processing
        /// each response message.
        /// </summary>
        /// <remarks>
        /// Generally, delaying acknowledgement allows for re-delivery by the broker in cases where the invoker crashes or restarts unexpectedly.
        /// However, MQTT acknowledgements must be delivered in order, so delaying these acknowledgements may affect the flow of acknowledgements
        /// being sent by other processes using this same MQTT client. Additionally, the MQTT broker has a limit on the number of un-acknowledged messages
        /// that are allowed to be in-flight at a single moment, so delaying too many acknowledgements may halt all further MQTT traffic on the underlying
        /// MQTT client.
        /// </remarks>
        public bool AutomaticallyAcknowledgeResponses { get; set; } = true;

        /// <summary>
        /// Invoke a streaming command on a particular streaming command executor
        /// </summary>
        /// <param name="requests">The stream of requests to send. This stream must contain at least one request.</param>
        /// <param name="streamMetadata">The metadata for the request stream as a whole.</param>
        /// <param name="additionalTopicTokenMap">Topic tokens to substitute in the request topic.</param>
        /// <param name="streamExchangeTimeout">The timeout between the beginning of the request stream and the end of both the request and response stream.</param>
        /// <param name="cancellationToken">Cancellation token. Signalling this will also make a single attempt to notify the executor of the cancellation.</param>
        /// <returns>The stream of responses.</returns>
        public async Task<IStreamContext<ReceivedStreamingExtendedResponse<TResp>>> InvokeStreamingCommandAsync(
            IAsyncEnumerable<StreamingExtendedRequest<TReq>> requests,
            RequestStreamMetadata? streamMetadata = null,
            Dictionary<string, string>? additionalTopicTokenMap = null,
            TimeSpan? streamExchangeTimeout = default,
            CancellationToken cancellationToken = default)
        {
            // TODO: Derive the request topic (like commandInvoker does)

            // TODO: Subscribe to the expected response topic

            // TODO: construct the IAsyncEnumerable of responses to capture the stream of responses prior to sending the first request.
            IAsyncEnumerable<StreamingExtendedResponse<TResp>> responses;
            IStreamContext<IAsyncEnumerable<StreamingExtendedResponse<TResp>>> streamContext;

            await foreach (var streamMessage in requests)
            {
                // TODO: Construct and send an MQTT message to the executor. Attach properties from both streamMetadata and streamMessage.Metadata
            }

            // TODO: Send the "end of stream" MQTT message now that all request messages have been sent

            throw new NotImplementedException();
        }

        public ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning restore CS0168 // Variable is declared but never used

    }
}
