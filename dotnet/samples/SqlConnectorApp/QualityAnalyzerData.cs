// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SqlQualityAnalyzerConnectorApp
{
    internal class QualityAnalyzerData
    {
        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("viscosity")]
        public double? Viscosity { get; set; }

        [JsonPropertyName("sweetness")]
        public double? Sweetness { get; set; }

        [JsonPropertyName("particleSize")]
        public double? ParticleSize { get; set; }

        [JsonPropertyName("overall")]
        public double? Overall { get; set; }
    }
}
