// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultSender
    {
        public DefaultSender()
        {
            Serializer = new();
        }

        public string? TelemetryName { get; set; }

        public DefaultSerializer Serializer { get; set; }

        public string? TelemetryTopic { get; set; }

        public string? TopicNamespace { get; set; }
    }
}
