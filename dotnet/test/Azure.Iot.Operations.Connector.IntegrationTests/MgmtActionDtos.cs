// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Azure.Iot.Operations.Connector.IntegrationTests
{
    /// <summary>JSON request/response DTOs that mirror the sample's
    /// <c>ManagementActionConnector.Contracts</c> types. Duplicated here on
    /// purpose so the integration test project does not take a project ref on
    /// the sample (matches the existing <see cref="RestThermostatStatus"/> /
    /// <see cref="TcpThermostatStatus"/> pattern).</summary>
    internal sealed record MgmtRebootRequest
    {
        [JsonPropertyName("force")]
        public bool Force { get; init; }
    }

    internal sealed record MgmtRebootResponse
    {
        [JsonPropertyName("requestId")]
        public Guid RequestId { get; init; }

        [JsonPropertyName("scheduledAtUtc")]
        public DateTime ScheduledAtUtc { get; init; }

        [JsonPropertyName("rebootCount")]
        public long RebootCount { get; init; }
    }

    internal sealed record MgmtTemperatureReading
    {
        [JsonPropertyName("value")]
        public double Value { get; init; }

        [JsonPropertyName("unit")]
        public string Unit { get; init; } = string.Empty;

        [JsonPropertyName("sampledAtUtc")]
        public DateTime SampledAtUtc { get; init; }
    }

    internal sealed record MgmtConfigurationUpdate
    {
        [JsonPropertyName("sampleIntervalMs")]
        public int SampleIntervalMs { get; init; }

        [JsonPropertyName("unit")]
        public string Unit { get; init; } = string.Empty;
    }

    internal sealed record MgmtConfigurationAck
    {
        [JsonPropertyName("appliedAtUtc")]
        public DateTime AppliedAtUtc { get; init; }

        [JsonPropertyName("appliedSampleIntervalMs")]
        public int AppliedSampleIntervalMs { get; init; }

        [JsonPropertyName("appliedUnit")]
        public string AppliedUnit { get; init; } = string.Empty;
    }
}
