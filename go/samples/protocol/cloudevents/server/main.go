// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"fmt"
	"log/slog"
	"net/url"
	"os"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/iso"
	"github.com/Azure/iot-operations-sdks/go/samples/protocol/cloudevents/envoy/dtmi_akri_samples_oven__1"
	"github.com/lmittmann/tint"
)

func main() {
	ctx := context.Background()
	log := slog.New(tint.NewHandler(os.Stdout, nil))
	app := must(protocol.NewApplication())

	mqttClient := mqtt.NewSessionClient(
		mqtt.TCPConnection("localhost", 1883),
		mqtt.WithSessionExpiry(600), // 10 minutes
	)
	server := must(dtmi_akri_samples_oven__1.NewOvenService(app, mqttClient))
	defer server.Close()

	check(mqttClient.Start())
	check(server.Start(ctx))

	fmt.Println("Press enter to quit.")
	ctx, cancel := context.WithCancel(ctx)
	go func() {
		must(fmt.Scanln())
		cancel()
	}()

	ce := &protocol.CloudEvent{Source: must(url.Parse("aio://oven/sample"))}

	started := iso.Time(time.Now())
	var counter int64
	for {
		externalTemperature := 100 - float64(counter)
		internalTemperature := 200 + float64(counter)
		server.SendTelemetryCollection(ctx, dtmi_akri_samples_oven__1.TelemetryCollection{
			ExternalTemperature: &externalTemperature,
			InternalTemperature: &internalTemperature,
		}, protocol.WithCloudEvent(ce))

		if counter%2 == 0 {
			duration := iso.Duration(time.Since(time.Time(started)))
			server.SendTelemetryCollection(ctx, dtmi_akri_samples_oven__1.TelemetryCollection{
				OperationSummary: &dtmi_akri_samples_oven__1.Object_OperationSummary{
					NumberOfCakes: &counter,
					StartingTime:  &started,
					TotalDuration: &duration,
				},
			})
		}

		log.Info("messages sent", "count", counter)
		counter++

		select {
		case <-time.After(time.Second):
		case <-ctx.Done():
			return
		}
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
