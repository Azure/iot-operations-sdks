// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseError
    {
        public TestErrorKind Kind { get; set; } = TestErrorKind.None;

        public string? Message { get; set; }

        public string? PropertyName { get; set; }

        public string? PropertyValue { get; set; }
    }
}
