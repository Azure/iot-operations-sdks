// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"fmt"
	"math"
	"os"
	"strconv"
	"time"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"

	"cmdserver/countercollection"
)

const counterServerId = "GoCounterServer"

type Handlers struct{
	counterValues map[string]int32
	counterLocations map[string]countercollection.CounterLocation
}

func (h *Handlers) Increment(
	ctx context.Context,
	req *protocol.CommandRequest[countercollection.IncrementRequestPayload],
) (*protocol.CommandResponse[countercollection.IncrementResponsePayload], error) {
	currentValue, ok := h.counterValues[req.Payload.CounterName]
	if !ok {
		condition := countercollection.CounterNotFound
		explanation := fmt.Sprintf("Go counter '%s' not found in counter collection", req.Payload.CounterName)
		return nil, &countercollection.CounterError{
			Condition: &condition,
			Explanation: &explanation,
		}
	}

	if currentValue == math.MaxInt32 {
		condition := countercollection.CounterOverflow
		explanation := fmt.Sprintf("Go counter '%s' has saturated; no further increment is possible", req.Payload.CounterName)
		return nil, &countercollection.CounterError{
			Condition: &condition,
			Explanation: &explanation,
		}
	}

	newValue := currentValue + 1
	h.counterValues[req.Payload.CounterName] = newValue

	return protocol.Respond(countercollection.IncrementResponsePayload{
		CounterValue: newValue,
	})
}

func (h *Handlers) GetLocation(
	ctx context.Context,
	req *protocol.CommandRequest[countercollection.GetLocationRequestPayload],
) (*protocol.CommandResponse[countercollection.GetLocationResponsePayload], error) {
	_, ok := h.counterValues[req.Payload.CounterName]
	if !ok {
		condition := countercollection.CounterNotFound
		explanation := fmt.Sprintf("Go counter '%s' not found in counter collection", req.Payload.CounterName)
		return nil, &countercollection.CounterError{
			Condition: &condition,
			Explanation: &explanation,
		}
	}

	counterLocation, ok := h.counterLocations[req.Payload.CounterName]

	if ok {
		return protocol.Respond(countercollection.GetLocationResponsePayload{
			CounterLocation: &counterLocation,
		})
	} else {
		return protocol.Respond(countercollection.GetLocationResponsePayload{
			CounterLocation: nil,
		})
	}
}

func main() {
	ctx := context.Background()
	app, err := protocol.NewApplication()
	if err != nil {
		panic(err)
	}

	if len(os.Args) < 2 {
		fmt.Printf("Usage: %s seconds_to_run", os.Args[0])
		return
	}

	secondsToRun, err := strconv.Atoi(os.Args[1])
	if err != nil {
		panic(err)
	}

	mqttClient, err := mqtt.NewSessionClient(
		counterServerId,
		mqtt.TCPConnection("localhost", 1883),
	)
	if err != nil {
		panic(err)
	}

	counterValues := make(map[string]int32)
	counterValues["alpha"] = 0
	counterValues["beta"] = 0

	counterLocations := make(map[string]countercollection.CounterLocation)
	counterLocations["alpha"] = countercollection.CounterLocation {
		Latitude: 14.4,
		Longitude: -123.0,
	}

	server, err := countercollection.NewCounterCollectionService(
		app,
		mqttClient,
		&Handlers{ counterValues, counterLocations },
	)
	if err != nil {
		panic(err)
	}

	defer server.Close()

	fmt.Printf("Connecting to MQTT broker as %s ... ", counterServerId)
	err = mqttClient.Start()
	if err != nil {
		panic(err)
	}
	fmt.Printf("Connected!\n")

	fmt.Printf("Starting server ... ")
	err = server.Start(ctx)
	if err != nil {
		panic(err)
	}
	fmt.Printf("server running for %d seconds\n", secondsToRun)

	time.Sleep(time.Duration(secondsToRun) * time.Second)

	fmt.Printf("Stopping server\n")
}
