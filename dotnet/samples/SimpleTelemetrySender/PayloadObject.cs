// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace SimpleTelemetrySender
{
    public class PayloadObject
    {
        [JsonPropertyName("SomeField")]
        public string? SomeField { get; set; }

        [JsonPropertyName("OtherField")]
        public string? OtherField { get; set; }
    }
}
