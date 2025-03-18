// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCase
    {
        public string? TestName { get; set; }

        public TestCaseDescription? Description { get; set; }

        public List<TestFeatureKind> Requires { get; set; } = new();

        public TestCasePrologue? Prologue { get; set; }

        public List<TestCaseAction> Actions { get; set; } = new();

        public TestCaseEpilogue? Epilogue { get; set; }
    }
}
