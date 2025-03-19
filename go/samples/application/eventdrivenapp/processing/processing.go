// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package processing

import (
	"context"
	"encoding/json"
	"sort"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/Azure/iot-operations-sdks/go/samples/application/eventdrivenapp/models"
)

func HandleSensorData(ctx context.Context, stateClient *statestore.Client[string, string], data models.SensorData) error {
	resp, err := stateClient.Get(ctx, models.StateStoreKey)
	if err != nil {
		var history models.SensorDataHistory
		historyJSON, err := json.Marshal(history)
		if err != nil {
			return err
		}

		_, err = stateClient.Set(ctx, models.StateStoreKey, string(historyJSON))
		if err != nil {
			return err
		}

		resp, err = stateClient.Get(ctx, models.StateStoreKey)
		if err != nil {
			return err
		}
	}

	var history models.SensorDataHistory
	if err := json.Unmarshal([]byte(resp.Value), &history); err != nil {
		return err
	}

	history = append(history, data)

	cutoff := time.Now().Add(-models.SlidingWindowSize * time.Second)
	newHistory := models.SensorDataHistory{}
	for _, item := range history {
		if !item.Timestamp.Before(cutoff) {
			newHistory = append(newHistory, item)
		}
	}

	historyJSON, err := json.Marshal(newHistory)
	if err != nil {
		return err
	}

	_, err = stateClient.Set(ctx, models.StateStoreKey, string(historyJSON))
	return err
}

func ProcessWindow(ctx context.Context, stateClient *statestore.Client[string, string], sender *protocol.TelemetrySender[models.WindowOutput]) error {
	resp, err := stateClient.Get(ctx, models.StateStoreKey)
	if err != nil {
		return err
	}

	var history models.SensorDataHistory
	if err := json.Unmarshal([]byte(resp.Value), &history); err != nil {
		return err
	}

	if len(history) == 0 {
		return nil
	}

	cutoff := time.Now().Add(-models.SlidingWindowSize * time.Second)
	windowData := models.SensorDataHistory{}
	for _, item := range history {
		if !item.Timestamp.Before(cutoff) {
			windowData = append(windowData, item)
		}
	}

	tempStats := calculateStats(func(data models.SensorData) float64 {
		return data.Temperature
	}, windowData)

	pressureStats := calculateStats(func(data models.SensorData) float64 {
		return data.Pressure
	}, windowData)

	vibrationStats := calculateStats(func(data models.SensorData) float64 {
		return data.Vibration
	}, windowData)

	output := models.WindowOutput{
		Timestamp:   time.Now(),
		WindowSize:  models.SlidingWindowSize,
		Temperature: tempStats,
		Pressure:    pressureStats,
		Vibration:   vibrationStats,
	}

	return sender.Send(ctx, output)
}

func calculateStats(valueSelector func(models.SensorData) float64, data models.SensorDataHistory) models.WindowStats {
	if len(data) == 0 {
		return models.WindowStats{}
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

	sortedValues := make([]float64, len(values))
	copy(sortedValues, values)
	sort.Float64s(sortedValues)

	var median float64
	if len(sortedValues)%2 == 0 {
		median = (sortedValues[len(sortedValues)/2-1] + sortedValues[len(sortedValues)/2]) / 2
	} else {
		median = sortedValues[len(sortedValues)/2]
	}

	return models.WindowStats{
		Min:    min,
		Max:    max,
		Mean:   sum / float64(len(values)),
		Median: median,
		Count:  len(values),
	}
}
