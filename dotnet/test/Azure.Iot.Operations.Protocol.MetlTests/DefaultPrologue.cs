// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultPrologue
    {
        public DefaultPrologue()
        {
            Executor = new();
            Invoker = new();
            Receiver = new();
            Sender = new();
        }

        public DefaultExecutor Executor { get; set; }

        public DefaultInvoker Invoker { get; set; }

        public DefaultReceiver Receiver { get; set; }

        public DefaultSender Sender { get; set; }
    }
}
