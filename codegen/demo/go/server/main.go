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

    "server/dtmi_codegen_communicationTest_jsonModel__1"
)

const serverId = "JsonGoServer"

func main() {
    ctx := context.Background()

    if len(os.Args) < 3 {
        fmt.Printf("Usage: %s {JSON} iterations [interval_in_seconds]", os.Args[0]);
        return;
    }

    if !strings.EqualFold(os.Args[1], "json") {
        fmt.Printf("format must be JSON");
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

    mqttClient := mqtt.NewSessionClient(
        mqtt.TCPConnection("localhost", 1883),
        mqtt.WithClientID(serverId),
    )

    server, err := dtmi_codegen_communicationTest_jsonModel__1.NewJsonModelService(mqttClient)
    if err != nil {
        panic(err)
    }

    defer server.Close()

    fmt.Printf("Connecting to MQTT broker as %s ... ", serverId)
    err = mqttClient.Start()
    if err != nil {
        panic(err)
    }
    fmt.Printf("Connected!\n")

    fmt.Printf("Starting send loop\n\n")

    err = server.Start(ctx)
    if err != nil {
        panic(err)
    }

    for i := 0; i < iterations; i++ {

        course := "Math"
        credit := iso.Duration(time.Duration(i + 2) * time.Hour + time.Duration(i + 1) * time.Minute + time.Duration(i) * time.Second)

        var proximity dtmi_codegen_communicationTest_jsonModel__1.Enum_Proximity
        if i % 3 == 0 {
            proximity = dtmi_codegen_communicationTest_jsonModel__1.Far
        } else {
            proximity = dtmi_codegen_communicationTest_jsonModel__1.Near
        }

        telemetry := dtmi_codegen_communicationTest_jsonModel__1.TelemetryCollection{
            Schedule: &dtmi_codegen_communicationTest_jsonModel__1.Object_Schedule{
                Course: &course,
                Credit: &credit,
            },

            Lengths: []float64{float64(i), float64(i + 1), float64(i + 2)},

            Proximity: &proximity,
        }

        fmt.Printf("  Sending iteration %d\n", i)
        err = server.SendTelemetryCollection(ctx, telemetry)
        if err != nil {
            panic(err)
        }

        time.Sleep(time.Duration(interval_in_seconds) * time.Second)
    }

    fmt.Printf("\nStopping send loop\n")
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
