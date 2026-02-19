// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace SimpleTelemetrySender
{
    public class PayloadObject
    {
        [JsonPropertyName("someField")]
        public string? SomeField { get; set; }

        [JsonPropertyName("otherField")]
        public string? OtherField { get; set; }
    }
}
