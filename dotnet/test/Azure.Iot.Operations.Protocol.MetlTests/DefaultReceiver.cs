// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultReceiver
    {
        public DefaultReceiver()
        {
            Serializer = new();
        }

        public DefaultSerializer Serializer { get; set; }

        public string? TelemetryTopic { get; set; }

        public string? TopicNamespace { get; set; }
    }
}
