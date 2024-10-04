// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace EventDrivenApp;

class InputSensorData
{
    required public string SensorId { get; set; }
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double Vibration { get; set; }
    public int MessageNumber { get; set; }
}
