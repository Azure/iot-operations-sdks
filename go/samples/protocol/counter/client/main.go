package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/counter/envoy/dtmi_com_example_Counter__1"
	"github.com/lmittmann/tint"
)

func main() {
	ctx := context.Background()
	slog.SetDefault(slog.New(tint.NewHandler(os.Stdout, nil)))

	mqttClient := must(mqtt.NewSessionClientFromEnv())

	counterServer := os.Getenv("COUNTER_SERVER_ID")

	fmt.Printf("Initialized MQTT client. Connecting to MQTT broker and calling to %s\n", counterServer)

	client := must(dtmi_com_example_Counter__1.NewCounterClient(mqttClient, protocol.WithResponseTopicPrefix("response")))
	defer client.Close()

	check(mqttClient.Start())
	check(client.Start(ctx))

	resp := must(client.ReadCounter(ctx, counterServer))

	fmt.Println("Counter value:", resp.Payload.CounterResponse)

	for range 15 {
		respIncr := must(client.Increment(ctx, counterServer))
		fmt.Println("Counter value after increment:", respIncr.Payload.CounterResponse)
	}

	fmt.Println("Press enter to quit.")
	must(fmt.Scanln())
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
