// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace ManagementActionConnector.Contracts
{
    /// <summary>Request body for the "reboot" Call action.</summary>
    public sealed record RebootRequest
    {
        /// <summary>If true, force a reboot even if one is already in progress.</summary>
        [JsonPropertyName("force")]
        public bool Force { get; init; }
    }

    /// <summary>Response body for the "reboot" Call action.</summary>
    public sealed record RebootResponse
    {
        [JsonPropertyName("requestId")]
        public Guid RequestId { get; init; }

        [JsonPropertyName("scheduledAtUtc")]
        public DateTime ScheduledAtUtc { get; init; }

        [JsonPropertyName("rebootCount")]
        public long RebootCount { get; init; }
    }

    /// <summary>Response body for the "readTemperature" Read action.</summary>
    public sealed record TemperatureReading
    {
        [JsonPropertyName("value")]
        public double Value { get; init; }

        [JsonPropertyName("unit")]
        public required string Unit { get; init; }

        [JsonPropertyName("sampledAtUtc")]
        public DateTime SampledAtUtc { get; init; }
    }

    /// <summary>Request body for the "writeConfiguration" Write action.</summary>
    public sealed record ConfigurationUpdate
    {
        [JsonPropertyName("sampleIntervalMs")]
        public int SampleIntervalMs { get; init; }

        [JsonPropertyName("unit")]
        public required string Unit { get; init; }
    }

    /// <summary>Response body for the "writeConfiguration" Write action.</summary>
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
