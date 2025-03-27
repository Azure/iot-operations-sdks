// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"fmt"
	"os"
	"strings"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"

	"cmdclient/countercollection"
)

const counterClientId = "GoCounterClient"

func main() {
	ctx := context.Background()
	app, err := protocol.NewApplication()
	if err != nil {
		panic(err)
	}

	if len(os.Args) < 3 {
		fmt.Printf("Usage: %s {INC|GET} counter_name\n", os.Args[0])
		return
	}

	counterName := os.Args[2]

	mqttClient, err := mqtt.NewSessionClient(
		counterClientId,
		mqtt.TCPConnection("localhost", 1883),
	)
	if err != nil {
		panic(err)
	}

	fmt.Printf("Connecting to MQTT broker as %s ... ", counterClientId)
	err = mqttClient.Start()
	if err != nil {
		panic(err)
	}
	fmt.Printf("Connected!\n")

	cmdClient, err := countercollection.NewCounterCollectionClient(
		app,
		mqttClient,
	)
	if err != nil {
		panic(err)
	}

	defer cmdClient.Close()

	err = cmdClient.Start(ctx)
	if err != nil {
		panic(err)
	}

	switch strings.ToLower(os.Args[1]) {
		case "inc":
		    var incResponse *protocol.CommandResponse[countercollection.IncrementResponsePayload]
			incResponse, err = cmdClient.Increment(ctx, countercollection.IncrementRequestPayload{
				CounterName: counterName,
			})
			if err == nil {
				fmt.Printf("New value = %d\n", incResponse.Payload.CounterValue)
				return
			}
		case "get":
		    var getResponse *protocol.CommandResponse[countercollection.GetLocationResponsePayload]
			getResponse, err = cmdClient.GetLocation(ctx, countercollection.GetLocationRequestPayload{
				CounterName: counterName,
			})
			if err == nil {
				if getResponse.Payload.CounterLocation != nil {
					fmt.Printf("Location = (%f, %f)\n", getResponse.Payload.CounterLocation.Latitude, getResponse.Payload.CounterLocation.Longitude)
				} else {
					fmt.Printf("counter is not deployed in the field\n")
				}
				return
			}
		default:
			fmt.Printf("command must be INC or GET")
			return
	}

	fmt.Printf("Request failed with error %s\n", err.Error())

	counterError, ok := err.(*countercollection.CounterError)
	if ok && counterError.Condition != nil {
		switch *counterError.Condition {
		case countercollection.CounterNotFound:
			fmt.Printf("Counter '%s' was not found\n", counterName)
		case countercollection.CounterOverflow:
			fmt.Printf("Counter '%s' has overflowed\n", counterName)
		}
	}
}
