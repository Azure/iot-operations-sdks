package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/samples/cloudevents/envoy/dtmi_akri_samples_oven__1"
	"github.com/lmittmann/tint"
)

type Handlers struct{}

func main() {
	ctx := context.Background()
	log := slog.New(tint.NewHandler(os.Stdout, nil))

	mqttClient := must(mqtt.NewSessionClient(
		mqtt.TCPConnection("localhost", 1883),
		mqtt.WithSessionExpiryInterval(10*60), // 10 minutes
	))
	client := must(dtmi_akri_samples_oven__1.NewOvenClient(
		mqttClient,
		func(
			_ context.Context,
			msg *protocol.TelemetryMessage[dtmi_akri_samples_oven__1.TelemetryCollection],
		) error {
			p := msg.Payload
			if p.ExternalTemperature != nil && p.InternalTemperature != nil {
				log.Info("temperature",
					"external", *p.ExternalTemperature,
					"internal", *p.InternalTemperature,
				)
			}

			os := p.OperationSummary
			if os != nil && os.NumberOfCakes != nil && os.StartingTime != nil && os.TotalDuration != nil {
				log.Info("operation summary",
					"number_of_cakes", *os.NumberOfCakes,
					"starting_time", os.StartingTime.String(),
					"total_duration", os.TotalDuration.String(),
				)
			}

			ce := msg.CloudEvent
			if ce != nil {
				log.LogAttrs(ctx, slog.LevelInfo, "cloud event", ce.Attrs()...)
			}
			return nil
		},
	))
	defer client.Close()

	check(mqttClient.Start())
	check(client.Start(ctx))

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
