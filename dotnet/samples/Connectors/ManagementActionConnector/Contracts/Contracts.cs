// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ManagementActionConnector.Contracts
{
    /// <summary>Request body for the "identify" Call action.</summary>
    public sealed record IdentifyRequest
    {
        /// <summary>How many times to blink the locator indicator (1-10).</summary>
        [JsonPropertyName("blinkCount")]
        public int BlinkCount { get; init; } = 3;
    }

    /// <summary>Response body for the "identify" Call action.</summary>
    public sealed record IdentifyResponse
    {
        [JsonPropertyName("requestId")]
        public Guid RequestId { get; init; }

        [JsonPropertyName("blinkCount")]
        public int BlinkCount { get; init; }

        [JsonPropertyName("identifyCount")]
        public long IdentifyCount { get; init; }
    }

    /// <summary>Response body for the "read-temperature" Read action.</summary>
    public sealed record TemperatureReading
    {
        [JsonPropertyName("value")]
        public double Value { get; init; }

        [JsonPropertyName("unit")]
        public required string Unit { get; init; }

        [JsonPropertyName("sampledAtUtc")]
        public DateTime SampledAtUtc { get; init; }
    }

    /// <summary>Request body for the "write-configuration" Write action.</summary>
    public sealed record ConfigurationUpdate
    {
        [JsonPropertyName("sampleIntervalMs")]
        public int SampleIntervalMs { get; init; }

        [JsonPropertyName("unit")]
        public required string Unit { get; init; }
    }

    /// <summary>Response body for the "write-configuration" Write action.</summary>
    public sealed record ConfigurationAck
    {
        [JsonPropertyName("appliedAtUtc")]
        public DateTime AppliedAtUtc { get; init; }

        [JsonPropertyName("appliedSampleIntervalMs")]
        public int AppliedSampleIntervalMs { get; init; }

        [JsonPropertyName("appliedUnit")]
        public required string AppliedUnit { get; init; }
    }
}
