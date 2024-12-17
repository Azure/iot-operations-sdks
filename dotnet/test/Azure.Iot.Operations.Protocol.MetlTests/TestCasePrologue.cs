// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCasePrologue
    {
        public TestCaseMqttConfig? MqttConfig { get; set; }

        public TestCasePushAcks? PushAcks { get; set; }

        public List<TestCaseExecutor> Executors { get; set; } = new();

        public List<TestCaseInvoker> Invokers { get; set; } = new();

        public List<TestCaseReceiver> Receivers { get; set; } = new();

        public List<TestCaseSender> Senders { get; set; } = new();

        public TestCaseCatch? Catch { get; set; }

        public Dictionary<string, int> CountdownEvents { get; set; } = new();
    }
}
