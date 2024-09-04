package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/counter/envoy/dtmi_com_example_Counter__1"
	"github.com/lmittmann/tint"
)

func main() {
	ctx := context.Background()
	slog.SetDefault(slog.New(tint.NewHandler(os.Stdout, nil)))

	clientID := fmt.Sprintf("sampleCounterClient-%d", time.Now().UnixMilli())
	fmt.Printf("Starting copunter client with clientId%s\n", clientID)
	mqttClient := must(mqtt.NewSessionClientFromEnv())

	check(mqttClient.Connect(ctx))

	counterServer := os.Getenv("COUNTER_SERVER_ID")

	fmt.Printf("Connected to MQTT broker, calling to %s\n", counterServer)

	client := must(dtmi_com_example_Counter__1.NewCounterClient(mqttClient, protocol.WithResponseTopicPrefix("response")))
	done := must(client.Listen(ctx))
	defer done()

	resp := must(client.ReadCounter(ctx, counterServer))

	fmt.Println("Counter value:", resp.Payload.CounterResponse)

	for i := 0; i < 15; i++ {
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
