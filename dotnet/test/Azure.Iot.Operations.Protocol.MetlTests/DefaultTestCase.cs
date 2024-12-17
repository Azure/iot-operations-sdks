// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultTestCase
    {
        public DefaultTestCase()
        {
            Prologue = new();
            Actions = new();
        }

        public DefaultPrologue Prologue { get; set; }

        public DefaultAction Actions { get; set; }
    }
}
