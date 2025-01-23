// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"log/slog"
	"os"
	"reflect"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/complex/envoy"
	complex "github.com/Azure/iot-operations-sdks/go/samples/protocol/complex/envoy/dtmi_example_Complex__1"
	"github.com/lmittmann/tint"
)

var telemetry chan complex.TelemetryCollection

func main() {
	ctx := context.Background()
	slog.SetDefault(slog.New(tint.NewHandler(os.Stdout, &tint.Options{
		Level: slog.LevelDebug,
	})))
	app := must(protocol.NewApplication(protocol.WithLogger(slog.Default())))

	mqttClient := must(mqtt.NewSessionClientFromEnv(mqtt.WithLogger(slog.Default())))
	serverID := os.Getenv("COMPLEX_SERVER_ID")
	slog.Info("initialized MQTT client", "server_id", serverID)

	client := must(complex.NewComplexClient(
		app,
		mqttClient,
		handleTelemetry,
		protocol.WithResponseTopicPrefix("response"),
	))
	defer client.Close()

	check(mqttClient.Start())
	check(client.Start(ctx))

	telemetry = make(chan complex.TelemetryCollection)

	res := must(client.GetTemperatures(ctx, serverID, envoy.Request))

	if !reflect.DeepEqual(res.Payload, envoy.Response) {
		panic("unexpected response")
	}

	if !reflect.DeepEqual(<-telemetry, envoy.Telemetry) {
		panic("unexpected telemetry")
	}
}

func handleTelemetry(ctx context.Context, msg *protocol.TelemetryMessage[complex.TelemetryCollection]) error {
	telemetry <- msg.Payload
	return nil
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
