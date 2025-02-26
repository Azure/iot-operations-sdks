// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseActionSync : TestCaseAction
    {
        public string? SignalEvent { get; set; }

        public string? WaitEvent { get; set; }
    }
}
