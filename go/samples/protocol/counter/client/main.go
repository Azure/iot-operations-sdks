// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"log/slog"
	"os"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/counter/envoy/dtmi_com_example_Counter__1"
	"github.com/lmittmann/tint"
)

func main() {
	ctx := context.Background()
	slog.SetDefault(slog.New(tint.NewHandler(os.Stdout, &tint.Options{
		Level: slog.LevelDebug,
	})))

	mqttClient := must(mqtt.NewSessionClientFromEnv(
		mqtt.WithLogger(slog.Default()),
	))
	counterServerID := os.Getenv("COUNTER_SERVER_ID")
	slog.Info("initialized MQTT client", "counter_server_id", counterServerID)

	client := must(dtmi_com_example_Counter__1.NewCounterClient(
		mqttClient,
		protocol.WithResponseTopicPrefix("response"),
		protocol.WithLogger(slog.Default()),
	))
	defer client.Close()

	check(mqttClient.Start())
	check(client.Start(ctx))

	resp := must(client.ReadCounter(ctx, counterServerID))

	slog.Info("read counter", "value", resp.Payload.CounterResponse)

	for range 15 {
		respIncr := must(client.Increment(ctx, counterServerID))
		slog.Info("increment", "value", respIncr.Payload.CounterResponse)
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
