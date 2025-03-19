// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"log/slog"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/Azure/iot-operations-sdks/go/samples/application/eventdrivenapp/internal/models"
	"github.com/Azure/iot-operations-sdks/go/samples/application/eventdrivenapp/internal/processing"
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

	log.Info("Initializing state store client...")
	stateStoreClient := must(statestore.New[string, string](app, mqttClient, statestore.WithLogger(log)))
	defer stateStoreClient.Close()

	log.Info("Creating input receiver...")
	inputReceiver, err := protocol.NewTelemetryReceiver(
		app,
		mqttClient,
		protocol.JSON[models.SensorData]{},
		models.SensorDataTopic,
		func(ctx context.Context, msg *protocol.TelemetryMessage[models.SensorData]) error {
			log.Info("Received sensor data", 
				"temp", msg.Payload.Temperature, 
				"pressure", msg.Payload.Pressure, 
				"vibration", msg.Payload.Vibration)
			return processing.HandleSensorData(ctx, stateStoreClient, msg.Payload)
		},
		protocol.WithLogger(log),
	)
	if err != nil {
		log.Error("Failed to create input telemetry receiver", "error", err)
		os.Exit(1)
	}
	defer inputReceiver.Close()

	log.Info("Creating output sender...")
	outputSender, err := protocol.NewTelemetrySender(
		app,
		mqttClient,
		protocol.JSON[models.WindowOutput]{},
		models.SensorWindowTopic,
		protocol.WithLogger(log),
	)
	if err != nil {
		log.Error("Failed to create output telemetry sender", "error", err)
		os.Exit(1)
	}
	defer outputSender.Close()

	log.Info("Starting MQTT connection...")
	check(mqttClient.Start())

	log.Info("Starting state store client...")
	check(stateStoreClient.Start(ctx))

	log.Info("Starting input worker...")
	check(inputReceiver.Start(ctx))

	log.Info("Starting window processing...")
	outputTicker := time.NewTicker(models.OutputPublishPeriod * time.Second)
	go func() {
		for {
			select {
			case <-outputTicker.C:
				if err := processing.ProcessWindow(ctx, stateStoreClient, outputSender); err != nil {
					log.Error("Error processing window", "error", err)
				} else {
					log.Info("Processed and published window statistics")
				}
			case <-ctx.Done():
				outputTicker.Stop()
				return
			}
		}
	}()

	log.Info("Output client started")
	log.Info("Listening for sensor data and publishing window statistics")

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGINT, syscall.SIGTERM)
	<-sig

	log.Info("Shutting down...")
	cancel()
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
