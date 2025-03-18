// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"

	"github.com/Azure/iot-operations-sdks/go/mqtt"
	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/Azure/iot-operations-sdks/go/services/schemaregistry"
	"github.com/lmittmann/tint"
)

const jsonSchema = `
{
	"$schema": "http://json-schema.org/draft-07/schema#",
	"type": "object",
	"properties": {
		"humidity": {
			"type": "integer"
		},
		"temperature": {
			"type": "number"
		}
	}
}
`

func main() {
	ctx := context.Background()
	log := slog.New(tint.NewHandler(os.Stdout, nil))
	app := must(protocol.NewApplication(protocol.WithLogger(log)))

	mqttClient := must(mqtt.NewSessionClient(
		"schemaregistrysample",
		mqtt.TCPConnection("localhost", 1883),
		mqtt.WithSessionExpiry(600), // 10 minutes
	))

	client := must(schemaregistry.New(app, mqttClient))
	defer client.Close()

	check(mqttClient.Start())
	check(client.Start(ctx))

	schema := must(client.Put(ctx, jsonSchema, schemaregistry.JSONSchemaDraft07))
	resolved := must(client.Get(ctx, *schema.Name))
	if resolved == nil {
		panic("schema not found")
	}

	fmt.Printf("%s %s", *resolved.Name, *resolved.Version)
	fmt.Println(*resolved.SchemaContent)

	notfound := must(client.Get(ctx, "not found"))
	if notfound != nil {
		panic("not-found schema found")
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
