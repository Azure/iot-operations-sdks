// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.MetlTests
{
    public class TestCaseSerializer
    {
        public static string? DefaultOutContentType;
        public static List<string> DefaultAcceptContentTypes = new();
        public static bool DefaultIndicateCharacterData;
        public static bool DefaultAllowCharacterData;
        public static bool DefaultFailDeserialization;

        public string? OutContentType { get; set; } = DefaultOutContentType;

        public List<string> AcceptContentTypes { get; set; } = DefaultAcceptContentTypes;

        public bool IndicateCharacterData { get; set; } = DefaultIndicateCharacterData;

        public bool AllowCharacterData { get; set; } = DefaultAllowCharacterData;

        public bool FailDeserialization { get; set; } = DefaultFailDeserialization;
    }
}
