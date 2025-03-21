// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"encoding/json"
	"sort"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
)

func HandleSensorData(ctx context.Context, stateClient *statestore.Client[string, []byte], data SensorData) error {
	resp, err := stateClient.Get(ctx, StateStoreKey)

	var history SensorDataHistory
	if err != nil || resp.Value == nil || len(resp.Value) == 0 {
		historyJSON, err := json.Marshal(history)
		if err != nil {
			return err
		}

		_, err = stateClient.Set(ctx, StateStoreKey, historyJSON)
		if err != nil {
			return err
		}
	} else {
		if err := json.Unmarshal(resp.Value, &history); err != nil {
			history = SensorDataHistory{}
		}
	}

	history = append(history, data)

	cutoff := time.Now().Add(-SlidingWindowSize * time.Second)
	newHistory := SensorDataHistory{}
	for _, item := range history {
		if !item.Timestamp.Before(cutoff) {
			newHistory = append(newHistory, item)
		}
	}

	historyJSON, err := json.Marshal(newHistory)
	if err != nil {
		return err
	}

	_, err = stateClient.Set(ctx, StateStoreKey, historyJSON)
	return err
}

func ProcessPublishWindow(ctx context.Context, stateClient *statestore.Client[string, []byte], sender *protocol.TelemetrySender[WindowOutput]) error {
	resp, err := stateClient.Get(ctx, StateStoreKey)
	if err != nil {
		return err
	}

	var history SensorDataHistory
	if err := json.Unmarshal([]byte(resp.Value), &history); err != nil {
		return err
	}

	if len(history) == 0 {
		return nil
	}

	now := time.Now()
	cutoff := now.Add(-SlidingWindowSize * time.Second)
	windowData := SensorDataHistory{}
	for _, item := range history {
		if !item.Timestamp.Before(cutoff) {
			windowData = append(windowData, item)
		}
	}

	tempStats := calculateStats(func(data SensorData) float64 {
		return data.Temperature
	}, windowData)

	pressureStats := calculateStats(func(data SensorData) float64 {
		return data.Pressure
	}, windowData)

	vibrationStats := calculateStats(func(data SensorData) float64 {
		return data.Vibration
	}, windowData)

	output := WindowOutput{
		Timestamp:   now,
		WindowSize:  SlidingWindowSize,
		Temperature: tempStats,
		Pressure:    pressureStats,
		Vibration:   vibrationStats,
	}

	return sender.Send(ctx, output)
}

func calculateStats(valueSelector func(SensorData) float64, data SensorDataHistory) WindowStats {
	if len(data) == 0 {
		return WindowStats{}
	}

	values := make([]float64, len(data))
	for i, item := range data {
		values[i] = valueSelector(item)
	}

	min := values[0]
	max := values[0]
	sum := 0.0

	for _, v := range values {
		if v < min {
			min = v
		}
		if v > max {
			max = v
		}
		sum += v
	}

	sort.Float64s(values)
	var median float64
	if len(values)%2 == 0 {
		median = (values[len(values)/2-1] + values[len(values)/2]) / 2
	} else {
		median = values[len(values)/2]
	}

	return WindowStats{
		Min:    min,
		Max:    max,
		Mean:   sum / float64(len(values)),
		Median: median,
		Count:  len(values),
	}
}
