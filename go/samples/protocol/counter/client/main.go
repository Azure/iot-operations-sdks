package main

import (
	"context"
	"log/slog"
	"os"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/counter/envoy/dtmi_com_example_Counter__1"
	"github.com/lmittmann/tint"
)

var receivedTelemetryCount int

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
        nil,
        protocol.WithResponseTopicPrefix("response"),
        protocol.WithLogger(slog.Default()),
    ))
    defer client.Close()

    check(mqttClient.Start())
    check(client.Start(ctx))

    telemetryChan := make(chan *protocol.TelemetryMessage[dtmi_com_example_Counter__1.TelemetryCollection], 15)

    telemetryReceiver := must(dtmi_com_example_Counter__1.NewTelemetryCollectionReceiver(
        mqttClient,
        dtmi_com_example_Counter__1.TelemetryTopic,
        func(ctx context.Context, msg *protocol.TelemetryMessage[dtmi_com_example_Counter__1.TelemetryCollection]) error {
            receivedTelemetryCount++
            telemetryChan <- msg
            return nil
        },
    ))
    defer telemetryReceiver.Close()

    check(telemetryReceiver.Start(ctx))

    resp := must(client.ReadCounter(ctx, counterServerID))
    slog.Info("read counter", "value", resp.Payload.CounterResponse)

    for i := 0; i < 15; i++ {
        respIncr := must(client.Increment(ctx, counterServerID, dtmi_com_example_Counter__1.IncrementRequestPayload{
            IncrementValue: 1,
        }))
        slog.Info("increment", "value", respIncr.Payload.CounterResponse)
    }

    for i := 0; i < 15; i++ {
        select {
        case msg := <-telemetryChan:
            p := msg.Payload
            if p.CounterValue != nil {
                slog.Info("received telemetry", "counter_value", *p.CounterValue)
            }
            msg.Ack()
        case <-time.After(10 * time.Second):
            slog.Warn("timed out waiting for telemetry")
        }
    }

    if receivedTelemetryCount == 15 {
        slog.Info("received expected number of telemetry messages", "count", receivedTelemetryCount)
    } else {
        slog.Error("unexpected number of telemetry messages received", "expected", 15, "actual", receivedTelemetryCount)
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