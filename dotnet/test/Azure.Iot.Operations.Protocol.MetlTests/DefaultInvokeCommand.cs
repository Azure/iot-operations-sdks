// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultInvokeCommand
    {
        public string? CommandName { get; set; }

        public string? RequestValue { get; set; }

        public TestCaseDuration? Timeout { get; set; }
    }
}
