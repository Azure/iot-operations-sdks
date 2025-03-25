// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace EventDrivenApp;

public class WindowSensorData
{
    [JsonPropertyName("min")]
    public double Min { get; set; }

    [JsonPropertyName("max")]
    public double Max { get; set; }

    [JsonPropertyName("mean")]
    public double Mean { get; set; }

    [JsonPropertyName("medium")]
    public double Medium { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public class WindowData
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("window_size")]
    public int WindowSize { get; set; }

    [JsonPropertyName("temperature")]
    public required WindowSensorData Temperature { get; set; }

    [JsonPropertyName("pressure")]
    public required WindowSensorData Pressure { get; set; }

    [JsonPropertyName("vibration")]
    public required WindowSensorData Vibration { get; set; }
}
