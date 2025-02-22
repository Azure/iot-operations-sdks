// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseCloudEvent
    {
        internal bool Irrelevant { get; set; } = false;

        public string? Source { get; set; }

        public string? Type { get; set; }

        public string? SpecVersion { get; set; }

        public string? Id { get; set; }

        public object Time { get; set; } = false;

        public string? DataContentType { get; set; }

        public object Subject { get; set; } = false;

        public object DataSchema { get; set; } = false;
    }
}
