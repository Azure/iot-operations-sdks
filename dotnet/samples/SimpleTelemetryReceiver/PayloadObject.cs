// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SimpleTelemetryReceiver
{
    public class PayloadObject
    {
        [JsonPropertyName("SomeField")]
        public string? SomeField { get; set; }

        [JsonPropertyName("OtherField")]
        public string? OtherField { get; set; }
    }
}
