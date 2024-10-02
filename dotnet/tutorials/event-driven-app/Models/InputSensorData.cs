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