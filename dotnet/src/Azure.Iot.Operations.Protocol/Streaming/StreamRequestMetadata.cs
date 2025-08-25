// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Protocol.Models;

namespace Azure.Iot.Operations.Protocol.Streaming
{
    /// <summary>
    /// Metadata for the request stream as a whole.
    /// </summary>
    public class StreamRequestMetadata
    {
        public Guid CorrelationId { get; internal set; }

        public string? InvokerClientId { get; }

        public Dictionary<string, string> TopicTokens { get; } = new();

        public string? Partition { get; }

        public string? ContentType { get; internal set; }

        public MqttPayloadFormatIndicator PayloadFormatIndicator { get; internal set; }

        public StreamRequestMetadata()
        {
            InvokerClientId = null;
            TopicTokens = new();
        }

        internal StreamRequestMetadata(MqttApplicationMessage message, string topicPattern)
        {
            CorrelationId = message.CorrelationData != null && GuidExtensions.TryParseBytes(message.CorrelationData, out Guid? correlationId)
                ? correlationId!.Value
                : throw new ArgumentException($"Invalid property -- CorrelationData in request message is null or not parseable as a GUID", nameof(message));

            InvokerClientId = null;

            if (message.UserProperties != null)
            {
                foreach (MqttUserProperty property in message.UserProperties)
                {
                    switch (property.Name)
                    {
                        case AkriSystemProperties.SourceId:
                            InvokerClientId = property.Value;
                            break;
                        case "$partition":
                            Partition = property.Value;
                            break;
                    }
                }
            }

            TopicTokens = topicPattern != null ? MqttTopicProcessor.GetReplacementMap(topicPattern, message.Topic) : new Dictionary<string, string>();
        }

        internal void MarshalTo(MqttApplicationMessage message)
        {
            if (Partition != null)
            {
                message.AddUserProperty("$partition", Partition);
            }
        }
    }
}
