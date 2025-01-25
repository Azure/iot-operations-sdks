// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"log/slog"
	"os"
	"os/signal"
	"reflect"
	"syscall"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/complex/envoy"
	complex "github.com/Azure/iot-operations-sdks/go/samples/protocol/complex/envoy/dtmi_example_Complex__1"
	"github.com/lmittmann/tint"
)

type Handlers struct {
	*complex.TelemetryCollectionSender
}

func main() {
	handlers := &Handlers{}

	ctx := context.Background()
	slog.SetDefault(slog.New(tint.NewHandler(os.Stdout, &tint.Options{Level: slog.LevelDebug})))
	app := must(protocol.NewApplication(protocol.WithLogger(slog.Default())))

	mqttClient := must(mqtt.NewSessionClientFromEnv(mqtt.WithLogger(slog.Default())))
	serverID := os.Getenv("AIO_MQTT_CLIENT_ID")
	slog.Info("initialized MQTT client", "server_id", serverID)

	server := must(complex.NewComplexService(
		app,
		mqttClient,
		handlers,
	))
	defer server.Close()

	check(mqttClient.Start())
	check(server.Start(ctx))

	handlers.TelemetryCollectionSender = must(complex.NewTelemetryCollectionSender(
		app,
		mqttClient,
		complex.TelemetryTopic,
		protocol.WithLogger(slog.Default()),
	))

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGINT, syscall.SIGTERM)
	<-sig
}

func (h *Handlers) GetTemperatures(
	ctx context.Context,
	req *protocol.CommandRequest[complex.GetTemperaturesRequestPayload],
) (*protocol.CommandResponse[complex.GetTemperaturesResponsePayload], error) {
	if !reflect.DeepEqual(req.Payload, envoy.Request) {
		return protocol.Respond(complex.GetTemperaturesResponsePayload{})
	}

	h.TelemetryCollectionSender.SendTelemetryCollection(ctx, envoy.Telemetry)
	return protocol.Respond(envoy.Response)
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
