// Code generated by Azure.Iot.Operations.ProtocolCompiler v0.10.0.0; DO NOT EDIT.
package adrbaseservice

import (
	"github.com/Azure/iot-operations-sdks/go/protocol"
)

type AssetUpdateEventTelemetryReceiver struct {
	*protocol.TelemetryReceiver[AssetUpdateEventTelemetry]
}

func NewAssetUpdateEventTelemetryReceiver(
	app *protocol.Application,
	client protocol.MqttClient,
	topic string,
	handler protocol.TelemetryHandler[AssetUpdateEventTelemetry],
	opt ...protocol.TelemetryReceiverOption,
) (*AssetUpdateEventTelemetryReceiver, error) {
	var err error
	receiver := &AssetUpdateEventTelemetryReceiver{}

	var opts protocol.TelemetryReceiverOptions
	opts.Apply(
		opt,
		protocol.WithTopicTokens{
			"telemetryName":     "assetUpdateEvent",
		},
	)

	receiver.TelemetryReceiver, err = protocol.NewTelemetryReceiver(
		app,
		client,
		protocol.JSON[AssetUpdateEventTelemetry]{},
		topic,
		handler,
		&opts,
	)

	return receiver, err
}
