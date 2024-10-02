// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json;

namespace EventDrivenApp;

class OutputSensorData
{
    public class AggregatedSensorData
    {
        [JsonProperty(propertyName: "min")]
        public double Min { get; set; }

        [JsonProperty(propertyName: "max")]
        public double Max { get; set; }

        [JsonProperty(propertyName: "mean")]
        public double Mean { get; set; }

        [JsonProperty(propertyName: "medium")]
        public double Medium { get; set; }
        
        [JsonProperty(propertyName: "count")]
        public int Count { get; set; }
    }

    [JsonProperty(propertyName: "timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty(propertyName: "window_size")]
    public int WindowSize { get; set; }

    [JsonProperty(propertyName: "temperature")]
    public required AggregatedSensorData Temperature { get; set; }

    [JsonProperty(propertyName: "pressure")]
    public required AggregatedSensorData Pressure { get; set; }

    [JsonProperty(propertyName: "vibration")]
    public required AggregatedSensorData Vibration { get; set; }
}
