// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultInvoker
    {
        public string? CommandName { get; set; }

        public string? RequestTopic { get; set; }

        public string? TopicNamespace { get; set; }

        public string? ResponseTopicPrefix { get; set; }

        public string? ResponseTopicSuffix { get; set; }
    }
}
