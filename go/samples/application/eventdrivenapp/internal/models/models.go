// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package models

import (
    "time"
)

const (
    SensorDataTopic     = "sensor/data"
    SensorWindowTopic   = "sensor/window_data"
    StateStoreKey       = "sensor_data_history"
    SlidingWindowSize   = 60 // seconds
    OutputPublishPeriod = 10 // seconds
)

// SensorData represents raw sensor measurements
type SensorData struct {
    Timestamp   time.Time `json:"Timestamp"`
    Temperature float64   `json:"Temperature"`
    Pressure    float64   `json:"Pressure"`
    Vibration   float64   `json:"Vibration"`
}

// SensorDataHistory is a collection of sensor data points
type SensorDataHistory []SensorData

// WindowStats contains statistical information about a data series
type WindowStats struct {
    Min    float64 `json:"Min"`
    Max    float64 `json:"Max"`
    Mean   float64 `json:"Mean"`
    Median float64 `json:"Median"`
    Count  int     `json:"Count"`
}

// WindowOutput represents processed window statistics
type WindowOutput struct {
    Timestamp   time.Time   `json:"Timestamp"`
    WindowSize  int         `json:"WindowSize"`
    Temperature WindowStats `json:"Temperature"`
    Pressure    WindowStats `json:"Pressure"`
    Vibration   WindowStats `json:"Vibration"`
}
