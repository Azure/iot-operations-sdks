package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"
	"os/signal"
	"syscall"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/counter/envoy/dtmi_com_example_Counter__1"
	"github.com/lmittmann/tint"
)

var counterValue int = 0

func main() {
	ctx := context.Background()
	slog.SetDefault(slog.New(tint.NewHandler(os.Stdout, nil)))

	clientID := os.Getenv("AIO_MQTT_CLIENT_ID")
	fmt.Printf("Starting counter server with clientId %s\n", clientID)
	mqttClient := must(mqtt.NewSessionClientFromEnv())

	fmt.Println("Initialized MQTT client.")

	server := must(dtmi_com_example_Counter__1.NewCounterService(mqttClient, ReadCounter, Increment, Reset))
	defer server.Close()

	check(mqttClient.Start())
	check(server.Start(ctx))

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, syscall.SIGINT, syscall.SIGTERM)
	<-sig
}

func ReadCounter(
	ctx context.Context,
	req *protocol.CommandRequest[any],
) (*protocol.CommandResponse[dtmi_com_example_Counter__1.ReadCounterResponsePayload], error) {
	slog.Info(
		"--> Counter.Read",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)
	defer slog.Info(
		"<-- Counter.Read",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)

	return protocol.Respond(dtmi_com_example_Counter__1.ReadCounterResponsePayload{
		CounterResponse: int32(counterValue),
	})
}

func Increment(
	ctx context.Context,
	req *protocol.CommandRequest[any],
) (*protocol.CommandResponse[dtmi_com_example_Counter__1.IncrementResponsePayload], error) {
	slog.Info(
		"--> Counter.Increment",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)
	defer slog.Info(
		"<-- Counter.Increment",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)
	counterValue++
	return protocol.Respond(dtmi_com_example_Counter__1.IncrementResponsePayload{
		CounterResponse: int32(counterValue),
	})
}

func Reset(
	ctx context.Context,
	req *protocol.CommandRequest[any],
) (*protocol.CommandResponse[any], error) {
	slog.Info(
		"--> Counter.Reset",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)
	defer slog.Info(
		"<-- Counter.Reset",
		slog.String("id", req.CorrelationData),
		slog.String("client", req.ClientID),
	)
	counterValue = 0
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
