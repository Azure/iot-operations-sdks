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

    "client/dtmi_codegen_communicationTest_jsonModel__1"
)

const clientId = "JsonGoClient"

func main() {
    ctx := context.Background()

    if len(os.Args) < 3 {
        fmt.Printf("Usage: %s {JSON} seconds_to_run", os.Args[0]);
        return;
    }

    if !strings.EqualFold(os.Args[1], "json") {
        fmt.Printf("format must be JSON");
        return
    }

    secondsToRun, err := strconv.Atoi(os.Args[2])
    if err != nil {
        panic(err)
    }

    mqttClient := mqtt.NewSessionClient(
        mqtt.TCPConnection("localhost", 1883),
        mqtt.WithClientID(clientId),
    )

    client, err := dtmi_codegen_communicationTest_jsonModel__1.NewJsonModelClient(
        mqttClient,
        handleTelemetry,
    )
    if err != nil {
        panic(err)
    }

    defer client.Close()

    fmt.Printf("Connecting to MQTT broker as %s ... ", clientId)
    err = mqttClient.Start()
    if err != nil {
        panic(err)
    }
    fmt.Printf("Connected!\n")

    fmt.Printf("Starting receive loop\n\n")

    err = client.Start(ctx)
    if err != nil {
        panic(err)
    }

    time.Sleep(time.Duration(secondsToRun) * time.Second)

    fmt.Printf("Stopping receive loop\n")
}

func handleTelemetry(ctx context.Context, msg *protocol.TelemetryMessage[dtmi_codegen_communicationTest_jsonModel__1.TelemetryCollection]) error {
    fmt.Printf("Received telemetry....\n")

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
