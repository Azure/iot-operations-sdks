// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace EventDrivenApp;

public class SensorData
{
    [JsonPropertyName("sensor_id")]
    required public string SensorId { get; set; }

    [JsonPropertyName("timestamp")]
    required public DateTime Timestamp { get; set; }

    [JsonPropertyName("temperature")]
    required public double Temperature { get; set; }

    [JsonPropertyName("pressure")]
    required public double Pressure { get; set; }

    [JsonPropertyName("vibration")]
    required public double Vibration { get; set; }

    [JsonPropertyName("msg_number")]
    required public int MessageNumber { get; set; }
}
