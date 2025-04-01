// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"fmt"
	"os"
	"strconv"
	"strings"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/protocol/iso"

	"telemserver/custommodel"
	"telemserver/jsonmodel"
	"telemserver/rawmodel"
)

const jsonServerId = "JsonGoServer"
const rawServerId = "RawGoServer"
const customServerId = "CustomGoServer"

func main() {
	ctx := context.Background()
	app, err := protocol.NewApplication()
	if err != nil {
		panic(err)
	}

	if len(os.Args) < 3 {
		fmt.Printf("Usage: %s {JSON|RAW|CUSTOM} iterations [interval_in_seconds]", os.Args[0])
		return
	}

	iterations, err := strconv.Atoi(os.Args[2])
	if err != nil {
		panic(err)
	}

	interval_in_seconds := 1
	if len(os.Args) > 3 {
		interval_in_seconds, err = strconv.Atoi(os.Args[3])
		if err != nil {
			panic(err)
		}
	}

	switch strings.ToLower(os.Args[1]) {
	case "json":
		sendJson(ctx, app, iterations, interval_in_seconds)
	case "raw":
		sendRaw(ctx, app, iterations, interval_in_seconds)
	case "custom":
		sendCustom(ctx, app, iterations, interval_in_seconds)
	default:
		fmt.Printf("format must be JSON or RAW or CUSTOM")
		return
	}

	fmt.Printf("\nStopping send loop\n")
}

func getMqttClient(serverId string) *mqtt.SessionClient {
	mqttClient, err := mqtt.NewSessionClient(
		serverId,
		mqtt.TCPConnection("localhost", 1883),
	)
	if err != nil {
		panic(err)
	}

	fmt.Printf("Connecting to MQTT broker as %s ... ", serverId)
	err = mqttClient.Start()
	if err != nil {
		panic(err)
	}
	fmt.Printf("Connected!\n")

	fmt.Printf("Starting send loop\n\n")

	return mqttClient
}

func sendJson(ctx context.Context, app *protocol.Application, iterations int, interval_in_seconds int) {
	mqttClient := getMqttClient(jsonServerId)

	server, err := jsonmodel.NewJsonModelService(app, mqttClient)
	if err != nil {
		panic(err)
	}

	defer server.Close()

	err = server.Start(ctx)
	if err != nil {
		panic(err)
	}

	for i := 0; i < iterations; i++ {
		course := "Math"
		credit := iso.Duration(time.Duration(i+2)*time.Hour + time.Duration(i+1)*time.Minute + time.Duration(i)*time.Second)

		var proximity jsonmodel.ProximitySchema
		if i%3 == 0 {
			proximity = jsonmodel.Far
		} else {
			proximity = jsonmodel.Near
		}

		telemetry := jsonmodel.TelemetryCollection{
			Schedule: &jsonmodel.ScheduleSchema{
				Course: &course,
				Credit: &credit,
			},

			Lengths: []float64{float64(i), float64(i + 1), float64(i + 2)},

			Proximity: &proximity,
		}

		fmt.Printf("  Sending iteration %d\n", i)
		err = server.SendTelemetry(ctx, telemetry, protocol.WithTopicTokens{
			"myToken": "GoReplacement",
		})
		if err != nil {
			panic(err)
		}

		time.Sleep(time.Duration(interval_in_seconds) * time.Second)
	}
}

func sendRaw(ctx context.Context, app *protocol.Application, iterations int, interval_in_seconds int) {
	mqttClient := getMqttClient(rawServerId)

	server, err := rawmodel.NewRawModelService(app, mqttClient)
	if err != nil {
		panic(err)
	}

	defer server.Close()

	err = server.Start(ctx)
	if err != nil {
		panic(err)
	}

	for i := 0; i < iterations; i++ {
		telemetry := []byte(fmt.Sprintf("Sample data %d", i))

		fmt.Printf("  Sending iteration %d\n", i)
		err = server.SendTelemetry(ctx, telemetry, protocol.WithTopicTokens{
			"myToken": "GoReplacement",
		})
		if err != nil {
			panic(err)
		}

		time.Sleep(time.Duration(interval_in_seconds) * time.Second)
	}
}

func sendCustom(ctx context.Context, app *protocol.Application, iterations int, interval_in_seconds int) {
	mqttClient := getMqttClient(customServerId)

	server, err := custommodel.NewCustomModelService(app, mqttClient)
	if err != nil {
		panic(err)
	}

	defer server.Close()

	err = server.Start(ctx)
	if err != nil {
		panic(err)
	}

	for i := 0; i < iterations; i++ {
		telemetry := []byte(fmt.Sprintf("Sample data %d", i))

		fmt.Printf("  Sending iteration %d\n", i)
		err = server.SendTelemetry(
			ctx,
			protocol.Data{
				telemetry,
				"text/csv", 1},
			protocol.WithTopicTokens{
				"myToken": "GoReplacement",
			})
		if err != nil {
			panic(err)
		}

		time.Sleep(time.Duration(interval_in_seconds) * time.Second)
	}
}
