// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class TestCase
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("commandLine")]
        public TestCommandLine CommandLine { get; set; } = new();

        [JsonPropertyName("errors")]
        public TestError[] Errors { get; set; } = [];
    }
}
