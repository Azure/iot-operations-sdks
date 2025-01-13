// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseActionInvokeCommand : TestCaseAction
    {
        public static string? DefaultCommandName;
        public static string? DefaultRequestValue;
        public static TestCaseDuration? DefaultTimeout;

        public int? InvocationIndex { get; set; }

        public string? CommandName { get; set; } = DefaultCommandName;

        public Dictionary<string, string>? TopicTokenMap { get; set; }

        public TestCaseDuration? Timeout { get; set; } = DefaultTimeout;

        public string? RequestValue { get; set; } = DefaultRequestValue;

        public Dictionary<string, string>? Metadata { get; set; }
    }
}
