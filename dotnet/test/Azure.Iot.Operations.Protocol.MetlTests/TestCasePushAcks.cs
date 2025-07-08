// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCasePushAcks
    {
        public List<TestAckKind> Publish { get; set; } = new();

        public List<TestAckKind> Subscribe { get; set; } = new();

        public List<TestAckKind> Unsubscribe { get; set; } = new();
    }
}
