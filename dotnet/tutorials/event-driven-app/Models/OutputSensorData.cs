// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace EventDrivenApp;

class OutputSensorData
{
    public class AggregatedSensorData
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Mean { get; set; }
        public double Medium { get; set; }
        public int Count { get; set; }
    }

    public DateTime Timestamp { get; set; }
    public int WindowSize { get; set; }
    public required AggregatedSensorData Temperature { get; set; }
    public required AggregatedSensorData Pressure { get; set; }
    public required AggregatedSensorData Vibration { get; set; }
}
