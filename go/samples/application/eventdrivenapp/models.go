// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

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
	Timestamp   time.Time
	Temperature float64
	Pressure    float64
	Vibration   float64
}

// SensorDataHistory is a collection of sensor data points
type SensorDataHistory []SensorData

// WindowStats contains statistical information about a data series
type WindowStats struct {
	Min    float64
	Max    float64
	Mean   float64
	Median float64
	Count  int
}

// WindowOutput represents processed window statistics
type WindowOutput struct {
	Timestamp   time.Time
	WindowSize  int
	Temperature WindowStats
	Pressure    WindowStats
	Vibration   WindowStats
}
