// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"encoding/json"
	"log/slog"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/lmittmann/tint"
)

const (
	sensorDataTopic     = "sensor/data"
	sensorWindowTopic   = "sensor/window_data"
	stateStoreKey       = "sensor_data_history"
	slidingWindowSize   = 60 // seconds
	outputPublishPeriod = 10 // seconds
)

type SensorData struct {
	Timestamp   time.Time `json:"Timestamp"`
	Temperature float64   `json:"Temperature"`
	Pressure    float64   `json:"Pressure"`
	Vibration   float64   `json:"Vibration"`
}

type SensorDataHistory []SensorData

type WindowStats struct {
	Min    float64 `json:"Min"`
	Max    float64 `json:"Max"`
	Mean   float64 `json:"Mean"`
	Median float64 `json:"Median"`
	Count  int     `json:"Count"`
}

type WindowOutput struct {
	Timestamp   time.Time   `json:"Timestamp"`
	WindowSize  int         `json:"WindowSize"`
	Temperature WindowStats `json:"Temperature"`
	Pressure    WindowStats `json:"Pressure"`
	Vibration   WindowStats `json:"Vibration"`
}

func main() {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	log := slog.New(tint.NewHandler(os.Stdout, &tint.Options{
		Level: slog.LevelInfo,
	}))
	slog.SetDefault(log)

	app := must(protocol.NewApplication(protocol.WithLogger(log)))

	log.Info("Initializing MQTT client...")
	mqttClient := must(mqtt.NewSessionClientFromEnv(mqtt.WithLogger(log)))

	log.Info("Initializing state store client...")
	stateStoreClient := must(statestore.New[string, string](app, mqttClient, statestore.WithLogger(log)))
	defer stateStoreClient.Close()

	log.Info("Creating input worker...")
	inputReceiver, err := protocol.NewTelemetryReceiver(
		app,
		mqttClient,
		protocol.JSON[SensorData]{},
		sensorDataTopic,
		func(ctx context.Context, msg *protocol.TelemetryMessage[SensorData]) error {
			return handleSensorData(ctx, stateStoreClient, msg.Payload)
		},
		protocol.WithLogger(log),
	)
	if err != nil {
		log.Error("Failed to create input telemetry receiver", "error", err)
		os.Exit(1)
	}
	defer inputReceiver.Close()

	log.Info("Creating output worker...")
	outputSender, err := protocol.NewTelemetrySender(
		app,
		mqttClient,
		protocol.JSON[WindowOutput]{},
		sensorWindowTopic,
		protocol.WithLogger(log),
	)
	if err != nil {
		log.Error("Failed to create output telemetry sender", "error", err)
		os.Exit(1)
	}

	log.Info("Starting MQTT connection...")
	check(mqttClient.Start())

	log.Info("Starting state store client...")
	check(stateStoreClient.Start(ctx))

	log.Info("Starting input worker...")
	check(inputReceiver.Start(ctx))

	log.Info("Starting output worker...")
	outputTicker := time.NewTicker(outputPublishPeriod * time.Second)
	go func() {
		for {
			select {
			case <-outputTicker.C:
				if err := processWindow(ctx, stateStoreClient, outputSender); err != nil {
					log.Error("Error processing window", "error", err)
				}
			case <-ctx.Done():
				outputTicker.Stop()
				return
			}
		}
	}()

	log.Info("Event-driven app started")
	log.Info("Subscribe to sensor/window_data to see output")

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGINT, syscall.SIGTERM)
	<-sig

	log.Info("Shutting down...")
	cancel()
}

func handleSensorData(ctx context.Context, stateClient *statestore.Client[string, string], data SensorData) error {
	resp, err := stateClient.Get(ctx, stateStoreKey)
	if err != nil {
		var history SensorDataHistory
		historyJSON, err := json.Marshal(history)
		if err != nil {
			return err
		}

		_, err = stateClient.Set(ctx, stateStoreKey, string(historyJSON))
		if err != nil {
			return err
		}

		resp, err = stateClient.Get(ctx, stateStoreKey)
		if err != nil {
			return err
		}
	}

	var history SensorDataHistory
	if err := json.Unmarshal([]byte(resp.Value), &history); err != nil {
		return err
	}

	history = append(history, data)

	cutoff := time.Now().Add(-slidingWindowSize * time.Second)
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

	_, err = stateClient.Set(ctx, stateStoreKey, string(historyJSON))
	return err
}

func processWindow(ctx context.Context, stateClient *statestore.Client[string, string], sender *protocol.TelemetrySender[WindowOutput]) error {
	resp, err := stateClient.Get(ctx, stateStoreKey)
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

	cutoff := time.Now().Add(-slidingWindowSize * time.Second)
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
        Timestamp:   time.Now(),
        WindowSize:  slidingWindowSize,
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

	sortedValues := make([]float64, len(values))
	copy(sortedValues, values)
	sort.Float64s(sortedValues)

	var median float64
	if len(sortedValues)%2 == 0 {
		median = (sortedValues[len(sortedValues)/2-1] + sortedValues[len(sortedValues)/2]) / 2
	} else {
		median = sortedValues[len(sortedValues)/2]
	}

	return WindowStats{
		Min:    min,
		Max:    max,
		Mean:   sum / float64(len(values)),
		Median: median,
		Count:  len(values),
	}
}

func check(e error) {
	if e != nil {
		panic(e)
	}
}

func must[T any](t T, e error) T {
	check(e)
	return t
}
