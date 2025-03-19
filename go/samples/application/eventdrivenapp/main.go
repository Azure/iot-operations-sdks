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
	"github.com/Azure/iot-operations-sdks/go/samples/application/eventdrivenapp/models"
	"github.com/Azure/iot-operations-sdks/go/samples/application/eventdrivenapp/processing"
	"github.com/Azure/iot-operations-sdks/go/services/statestore"
	"github.com/lmittmann/tint"
)

func main() {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	log := slog.New(tint.NewHandler(os.Stdout, &tint.Options{
		Level: slog.LevelInfo,
	}))

	app, mqttClient, stateStoreClient := initializeComponents(log)
	defer stateStoreClient.Close()

	inputWorker := createInputWorker(ctx, app, mqttClient, stateStoreClient, log)
	defer inputWorker.Close()

	createOutputWorker(ctx, app, mqttClient, stateStoreClient, log)

	startServices(ctx, mqttClient, stateStoreClient, inputWorker, log)

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGINT, syscall.SIGTERM)
	<-sig

	log.Info("Shutting down...")
	cancel()
}

func initializeComponents(log *slog.Logger) (*protocol.Application, *mqtt.SessionClient, *statestore.Client[string, string]) {
	log.Info("Initializing application components...")

	app, err := protocol.NewApplication(protocol.WithLogger(log))
	if err != nil {
		log.Error("Failed to create protocol application", "error", err)
		os.Exit(1)
	}

	mqttClient, err := mqtt.NewSessionClientFromEnv(mqtt.WithLogger(log))
	if err != nil {
		log.Error("Failed to create MQTT client", "error", err)
		os.Exit(1)
	}

	stateStoreClient, err := statestore.New[string, string](app, mqttClient, statestore.WithLogger(log))
	if err != nil {
		log.Error("Failed to create state store client", "error", err)
		os.Exit(1)
	}

	return app, mqttClient, stateStoreClient
}

func createInputWorker(ctx context.Context, app *protocol.Application, mqttClient protocol.MqttClient,
	stateStoreClient *statestore.Client[string, string], log *slog.Logger) *protocol.TelemetryReceiver[models.SensorData] {

	log.Info("Creating input telemetry receiver...")
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

	return inputReceiver
}

func createOutputWorker(ctx context.Context, app *protocol.Application, mqttClient protocol.MqttClient,
	stateStoreClient *statestore.Client[string, string], log *slog.Logger) *protocol.TelemetrySender[models.WindowOutput] {

	log.Info("Creating output telemetry sender...")
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

	go runWindowProcessor(ctx, stateStoreClient, outputSender, log)

	return outputSender
}

func runWindowProcessor(ctx context.Context, stateStoreClient *statestore.Client[string, string],
	outputSender *protocol.TelemetrySender[models.WindowOutput], log *slog.Logger) {

	outputTicker := time.NewTicker(models.OutputPublishPeriod * time.Second)
	defer outputTicker.Stop()

	for {
		select {
		case <-outputTicker.C:
			if err := processing.ProcessWindow(ctx, stateStoreClient, outputSender); err != nil {
				log.Error("Error processing window", "error", err)
			} else {
				log.Info("Processed and published window statistics")
			}
		case <-ctx.Done():
			log.Info("Window processor shutting down")
			return
		}
	}
}

func startServices(ctx context.Context, mqttClient *mqtt.SessionClient,
	stateStoreClient *statestore.Client[string, string],
	inputReceiver *protocol.TelemetryReceiver[models.SensorData], log *slog.Logger) {

	log.Info("Starting MQTT connection...")
	if err := mqttClient.Start(); err != nil {
		log.Error("Failed to start MQTT connection", "error", err)
		os.Exit(1)
	}

	log.Info("Starting state store client...")
	if err := stateStoreClient.Start(ctx); err != nil {
		log.Error("Failed to start state store client", "error", err)
		os.Exit(1)
	}

	log.Info("Starting input receiver...")
	if err := inputReceiver.Start(ctx); err != nil {
		log.Error("Failed to start input receiver", "error", err)
		os.Exit(1)
	}

	log.Info("Application startup complete - listening for sensor data and publishing window statistics")
}
