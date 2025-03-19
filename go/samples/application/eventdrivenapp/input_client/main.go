// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"log/slog"
	"math/rand"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/application/eventdrivenapp/internal/models"
	"github.com/lmittmann/tint"
)

func main() {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	log := slog.New(tint.NewHandler(os.Stdout, &tint.Options{
		Level: slog.LevelInfo,
	}))
	slog.SetDefault(log)

	log.Info("Initializing application...")
	app := must(protocol.NewApplication(protocol.WithLogger(log)))

	log.Info("Initializing MQTT client...")
	mqttClient := must(mqtt.NewSessionClientFromEnv(mqtt.WithLogger(log)))

	log.Info("Creating sensor data sender...")
	sender, err := protocol.NewTelemetrySender(
		app,
		mqttClient,
		protocol.JSON[models.SensorData]{},
		models.SensorDataTopic,
		protocol.WithLogger(log),
	)
	if err != nil {
		log.Error("Failed to create telemetry sender", "error", err)
		os.Exit(1)
	}
	defer sender.Close()

	log.Info("Starting MQTT connection...")
	check(mqttClient.Start())

	log.Info("Starting sensor data generation...")
	ticker := time.NewTicker(1 * time.Second)
	go func() {
		for {
			select {
			case <-ticker.C:
				sensorData := generateSensorData()
				if err := sender.Send(ctx, sensorData); err != nil {
					log.Error("Failed to send sensor data", "error", err)
				} else {
					log.Info("Sent sensor data", 
						"temp", sensorData.Temperature, 
						"pressure", sensorData.Pressure, 
						"vibration", sensorData.Vibration)
				}
			case <-ctx.Done():
				ticker.Stop()
				return
			}
		}
	}()

	log.Info("Input client started - generating simulated sensor data")

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGINT, syscall.SIGTERM)
	<-sig

	log.Info("Shutting down...")
	cancel()
}

func generateSensorData() models.SensorData {
	return models.SensorData{
		Timestamp:   time.Now(),
		Temperature: 20 + 5*rand.Float64(),
		Pressure:    100 + 10*rand.Float64(),
		Vibration:   0.5 + 0.5*rand.Float64(),
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
