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

	"telemclient/custommodel"
	"telemclient/jsonmodel"
	"telemclient/rawmodel"
)

const jsonCientId = "JsonGoClient"
const rawCientId = "RawGoClient"
const customCientId = "CustomGoClient"

type TelemetryClient interface {
	Start(ctx context.Context) error
	Close()
}

func main() {
	ctx := context.Background()
	app, err := protocol.NewApplication()
	if err != nil {
		panic(err)
	}

	if len(os.Args) < 3 {
		fmt.Printf("Usage: %s {JSON|RAW|CUSTOM} seconds_to_run", os.Args[0]);
		return;
	}

	secondsToRun, err := strconv.Atoi(os.Args[2])
	if err != nil {
		panic(err)
	}

	var clientId string
	var mqttClient *mqtt.SessionClient
	var telemClient TelemetryClient

	switch strings.ToLower(os.Args[1]) {
		case "json":
			clientId = jsonCientId
			mqttClient, err = mqtt.NewSessionClient(
				clientId,
				mqtt.TCPConnection("localhost", 1883),
			)
			if err == nil {
				telemClient, err = jsonmodel.NewJsonModelClient(
					app,
					mqttClient,
					handleJsonTelemetry,
				)
			}
		case "raw":
			clientId = rawCientId
			mqttClient, err = mqtt.NewSessionClient(
				clientId,
				mqtt.TCPConnection("localhost", 1883),
			)
			if err == nil {
				telemClient, err = rawmodel.NewRawModelClient(
					app,
					mqttClient,
					handleRawTelemetry,
				)
			}
		case "custom":
			clientId = customCientId
			mqttClient, err = mqtt.NewSessionClient(
				clientId,
				mqtt.TCPConnection("localhost", 1883),
			)
			if err == nil {
				telemClient, err = custommodel.NewCustomModelClient(
					app,
					mqttClient,
					handleCustomTelemetry,
				)
			}
		default:
			fmt.Printf("format must be JSON or RAW or CUSTOM")
			return
	}

	if err != nil {
		panic(err)
	}

	defer telemClient.Close()

	fmt.Printf("Connecting to MQTT broker as %s ... ", clientId)
	err = mqttClient.Start()
	if err != nil {
		panic(err)
	}
	fmt.Printf("Connected!\n")

	fmt.Printf("Starting receive loop\n\n")

	err = telemClient.Start(ctx)
	if err != nil {
		panic(err)
	}

	time.Sleep(time.Duration(secondsToRun) * time.Second)

	fmt.Printf("Stopping receive loop\n")
}

func handleJsonTelemetry(ctx context.Context, msg *protocol.TelemetryMessage[jsonmodel.TelemetryCollection]) error {
	fmt.Printf("Received telemetry with token replacement %s....\n", msg.TopicTokens["ex:myToken"])

	p := msg.Payload

	if p.Schedule != nil {
		fmt.Printf("  Schedule: \"%s\" => %s\n", *p.Schedule.Course, *p.Schedule.Credit)
	}

	if p.Lengths != nil {
		fmt.Printf("  Lengths:")
		for _, length := range p.Lengths {
			fmt.Printf(" %f", length)
		}
		fmt.Printf("\n")
	}

	if p.Proximity != nil {
		fmt.Printf("  Proximity: %s\n", *p.Proximity)
	}

	fmt.Printf("\n")

	msg.Ack()
	return nil
}

func handleRawTelemetry(ctx context.Context, msg *protocol.TelemetryMessage[[]byte]) error {
	fmt.Printf("Received telemetry with token replacement %s....\n", msg.TopicTokens["ex:myToken"])

	fmt.Printf("  data: %s\n\n", string(msg.Payload))

	msg.Ack()
	return nil
}

func handleCustomTelemetry(ctx context.Context, msg *protocol.TelemetryMessage[protocol.Data]) error {
	fmt.Printf("Received telemetry with content type %s and token replacement %s....\n", msg.ContentType, msg.TopicTokens["ex:myToken"])

	fmt.Printf("  Payload: %s\n\n", string(msg.Payload.Payload))

	msg.Ack()
	return nil
}
