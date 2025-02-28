// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class DefaultSerializer
    {
        public string? OutContentType { get; set; }

        public List<string> AcceptContentTypes { get; set; } = new();

        public bool IndicateCharacterData { get; set; }

        public bool AllowCharacterData { get; set; }

        public bool FailDeserialization { get; set; }
    }
}
