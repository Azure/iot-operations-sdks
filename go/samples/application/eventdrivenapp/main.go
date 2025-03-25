// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"fmt"
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

type Application struct {
	app             *protocol.Application
	mqttClient      *mqtt.SessionClient
	stateStoreClient *statestore.Client[string, []byte]
	inputReceiver   *protocol.TelemetryReceiver[SensorData]
	outputSender    *protocol.TelemetrySender[WindowOutput]
	log             *slog.Logger
}

func main() {
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	log := slog.New(tint.NewHandler(os.Stdout, &tint.Options{
		Level: slog.LevelInfo,
	}))

	app, err := NewApplication(log)
	if err != nil {
		log.Error("failed to initialize application", "error", err)
		os.Exit(1)
	}
	defer app.Close()

	if err := app.SetupWorkers(ctx); err != nil {
		log.Error("failed to setup workers", "error", err)
		os.Exit(1)
	}

	if err := app.Start(ctx); err != nil {
		log.Error("failed to start application", "error", err)
		os.Exit(1)
	}

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGINT, syscall.SIGTERM)
	<-sig

	log.Info("shutting down...")
	cancel()
}

func NewApplication(log *slog.Logger) (*Application, error) {
	app, err := protocol.NewApplication(protocol.WithLogger(log))
	if err != nil {
		return nil, fmt.Errorf("failed to create protocol application: %w", err)
	}

	mqttClient, err := mqtt.NewSessionClientFromEnv(mqtt.WithLogger(log))
	if err != nil {
		return nil, fmt.Errorf("failed to create MQTT client: %w", err)
	}

	stateStoreClient, err := statestore.New[string, []byte](app, mqttClient, statestore.WithLogger(log))
	if err != nil {
		return nil, fmt.Errorf("failed to create state store client: %w", err)
	}

	return &Application{
		app:             app,
		mqttClient:      mqttClient,
		stateStoreClient: stateStoreClient,
		log:             log,
	}, nil
}

func (a *Application) SetupWorkers(ctx context.Context) error {
	var err error
	
	// Create input receiver
	a.inputReceiver, err = protocol.NewTelemetryReceiver(
		a.app,
		a.mqttClient,
		protocol.JSON[SensorData]{},
		SensorDataTopic,
		func(ctx context.Context, msg *protocol.TelemetryMessage[SensorData]) error {
			a.log.Info("received sensor data",
				"temp", msg.Payload.Temperature,
				"pressure", msg.Payload.Pressure,
				"vibration", msg.Payload.Vibration)
			return HandleSensorData(ctx, a.stateStoreClient, msg.Payload)
		},
		protocol.WithLogger(a.log),
	)
	if err != nil {
		return fmt.Errorf("failed to create input telemetry receiver: %w", err)
	}

	a.outputSender, err = protocol.NewTelemetrySender(
		a.app,
		a.mqttClient,
		protocol.JSON[WindowOutput]{},
		SensorWindowTopic,
		protocol.WithLogger(a.log),
	)
	if err != nil {
		return fmt.Errorf("failed to create output telemetry sender: %w", err)
	}

	go a.runWindowProcessor(ctx)
	
	return nil
}

func (a *Application) Start(ctx context.Context) error {
	a.log.Info("starting MQTT connection...")
	if err := a.mqttClient.Start(); err != nil {
		return fmt.Errorf("failed to start MQTT connection: %w", err)
	}

	a.log.Info("starting state store client...")
	if err := a.stateStoreClient.Start(ctx); err != nil {
		return fmt.Errorf("failed to start state store client: %w", err)
	}

	a.log.Info("starting input receiver...")
	if err := a.inputReceiver.Start(ctx); err != nil {
		return fmt.Errorf("failed to start input receiver: %w", err)
	}

	a.log.Info("application startup complete - listening for sensor data and publishing window statistics")
	return nil
}

func (a *Application) Close() {
	if a.inputReceiver != nil {
		a.inputReceiver.Close()
	}
	if a.stateStoreClient != nil {
		a.stateStoreClient.Close()
	}
}

func (a *Application) runWindowProcessor(ctx context.Context) {
	outputTicker := time.NewTicker(OutputPublishPeriod * time.Second)
	defer outputTicker.Stop()

	for {
		select {
		case <-outputTicker.C:
			if err := ProcessPublishWindow(ctx, a.stateStoreClient, a.outputSender); err != nil {
				a.log.Error("error processing window", "error", err)
			} else {
				a.log.Info("processed and published window statistics")
			}
		case <-ctx.Done():
			a.log.Info("window processor shutting down")
			return
		}
	}
}
