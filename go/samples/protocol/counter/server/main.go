// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"log/slog"
	"os"
	"os/signal"
	"sync/atomic"
	"syscall"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/counter/envoy/counter"
	"github.com/lmittmann/tint"
)

type Handlers struct {
	counterValue    int32
	telemetrySender *counter.TelemetrySender
}

func main() {
	handlers := &Handlers{}

	ctx := context.Background()
	slog.SetDefault(slog.New(tint.NewHandler(os.Stdout, &tint.Options{
		Level: slog.LevelDebug,
	})))
	app := must(protocol.NewApplication(protocol.WithLogger(slog.Default())))

	mqttClient := must(mqtt.NewSessionClientFromEnv(
		mqtt.WithLogger(slog.Default()),
	))
	counterServerID := os.Getenv("AIO_MQTT_CLIENT_ID")
	slog.Info("initialized MQTT client", "counter_server_id", counterServerID)

	server := must(counter.NewCounterService(
		app,
		mqttClient,
		handlers,
	))
	defer server.Close()

	check(mqttClient.Start())
	check(server.Start(ctx))

	sender := must(counter.NewTelemetrySender(
		app,
		mqttClient,
		counter.TelemetryTopic,
		protocol.WithLogger(slog.Default()),
	))
	handlers.telemetrySender = sender

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGINT, syscall.SIGTERM)
	<-sig
}

func (h *Handlers) ReadCounter(
	ctx context.Context,
	req *protocol.CommandRequest[any],
) (*protocol.CommandResponse[counter.ReadCounterResponsePayload], error) {
	slog.Info(
		"--> counter.Read",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)
	defer slog.Info(
		"<-- counter.Read",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)

	return protocol.Respond(counter.ReadCounterResponsePayload{
		CounterResponse: atomic.LoadInt32(&h.counterValue),
	})
}

func (h *Handlers) Increment(
	ctx context.Context,
	req *protocol.CommandRequest[counter.IncrementRequestPayload],
) (*protocol.CommandResponse[counter.IncrementResponsePayload], error) {
	slog.Info(
		"--> counter.Increment",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)
	defer slog.Info(
		"<-- counter.Increment",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)

	value := atomic.AddInt32(&h.counterValue, req.Payload.IncrementValue)
	telemetry := counter.TelemetryCollection{
		CounterValue: &value,
	}
	err := h.telemetrySender.SendTelemetry(ctx, telemetry)
	if err != nil {
		slog.Error("failed to send telemetry", "error", err)
	}

	return protocol.Respond(counter.IncrementResponsePayload{
		CounterResponse: atomic.AddInt32(&h.counterValue, 1),
	})
}

func (h *Handlers) Reset(
	ctx context.Context,
	req *protocol.CommandRequest[any],
) (*protocol.CommandResponse[any], error) {
	slog.Info(
		"--> counter.Reset",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)
	defer slog.Info(
		"<-- counter.Reset",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)

	atomic.StoreInt32(&h.counterValue, 0)
	return protocol.Respond[any](nil)
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
