// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace EventDrivenApp;

class InputSensorData
{
    [JsonProperty(propertyName: "sensor_id")]
    required public string SensorId { get; set; }

    [JsonProperty(propertyName: "timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty(propertyName: "temperature")]
    public double Temperature { get; set; }

    [JsonProperty(propertyName: "pressure")]
    public double Pressure { get; set; }

    [JsonProperty(propertyName: "vibration")]
    public double Vibration { get; set; }

    [JsonProperty(propertyName: "msg_number")]
    public int MessageNumber { get; set; }
}

class OutputSensorData
{
    public class AggregatedSensorData
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Mean { get; set; }
        public double Medium { get; set; }
        public double Count { get; set; }
    }

    public DateTime Timestamp { get; set; }
    public int WindowSize { get; set; }
    public required AggregatedSensorData Temperature { get; set; }
    public required AggregatedSensorData Pressure { get; set; }
    public required AggregatedSensorData Vibration { get; set; }
}
