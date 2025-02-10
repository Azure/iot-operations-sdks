// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.6.0.0; DO NOT EDIT.
package counter

import (
	"context"

	"github.com/Azure/iot-operations-sdks/go/protocol"
)

type TelemetrySender struct {
	*protocol.TelemetrySender[TelemetryCollection]
}

func NewTelemetrySender(
	app *protocol.Application,
	client protocol.MqttClient,
	topic string,
	opt ...protocol.TelemetrySenderOption,
) (*TelemetrySender, error) {
	var err error
	sender := &TelemetrySender{}

	var opts protocol.TelemetrySenderOptions
	opts.Apply(
		opt,
	)

	sender.TelemetrySender, err = protocol.NewTelemetrySender(
		app,
		client,
		protocol.JSON[TelemetryCollection]{},
		topic,
		&opts,
	)

	return sender, err
}

func (sender TelemetrySender) SendTelemetry(
	ctx context.Context,
	telemetry TelemetryCollection,
	opt ...protocol.SendOption,
) error {
	return sender.Send(ctx, telemetry, opt...)
}
