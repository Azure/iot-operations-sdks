// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"errors"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"slices"
	"strings"
	"testing"
	"time"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/BurntSushi/toml"
	"github.com/eclipse/paho.golang/packets"
	"github.com/google/uuid"
	"github.com/stretchr/testify/require"
	"gopkg.in/yaml.v3"
)

func RunTelemetryReceiverTests(t *testing.T) {
	var telemetryReceiverDefaultInfo DefaultTestCase

	_, err := toml.DecodeFile(
		"../../../eng/test/test-cases/Protocol/TelemetryReceiver/defaults.toml",
		&telemetryReceiverDefaultInfo,
	)
	if err != nil {
		panic(err)
	}

	TestCaseDefaultInfo = &telemetryReceiverDefaultInfo
	TestCaseDefaultSerializer = &telemetryReceiverDefaultInfo.Prologue.Receiver.Serializer

	files, err := filepath.Glob(
		"../../../eng/test/test-cases/Protocol/TelemetryReceiver/*.yaml",
	)
	if err != nil {
		log.Fatal(err)
	}

	enableFreezing()

	for ix, f := range files {
		testName, _ := strings.CutSuffix(filepath.Base(f), ".yaml")
		t.Run(testName, func(t *testing.T) {
			runOneTelemetryReceiverTest(t, ix, testName, f)
		})
	}
}

func runOneTelemetryReceiverTest(
	t *testing.T,
	testCaseIndex int,
	testName string,
	fileName string,
) {
	pendingTestCases := []string{
		"TelemetryReceiverReceivesWithCloudEventIdEmpty_NoCloudEvent",
		"TelemetryReceiverReceivesWithCloudEventTypeEmpty_NoCloudEvent",
	}

	testCaseYaml, err := os.ReadFile(fileName)
	if err != nil {
		log.Fatal(err)
	}

	//	t.Parallel()

	var testCase TestCase
	err = yaml.Unmarshal(testCaseYaml, &testCase)
	if err != nil {
		log.Fatal(err)
	}

	if slices.Contains(testCase.Requires, Unobtanium) ||
		slices.Contains(testCase.Requires, ExplicitDefault) {
		t.Skipf(
			"Skipping test %s because it requires an unavailable feature",
			testName,
		)
	}

	if slices.Contains(pendingTestCases, testName) {
		t.Skipf(
			"Skipping test %s because it requires a feature which has not yet been implemented",
			testName,
		)
	}

	var mqttClientID string
	if testCase.Prologue.MqttConfig.ClientID != nil {
		mqttClientID = *testCase.Prologue.MqttConfig.ClientID
	} else {
		mqttClientID = fmt.Sprintf("ReceiverTestClient%d", testCaseIndex)
	}

	stubBroker, sessionClient := getStubAndSessionClient(t, mqttClientID)

	for _, ackKind := range testCase.Prologue.PushAcks.Publish {
		stubBroker.EnqueuePubAck(ackKind)
	}

	for _, ackKind := range testCase.Prologue.PushAcks.Subscribe {
		stubBroker.EnqueueSubAck(ackKind)
	}

	for _, ackKind := range testCase.Prologue.PushAcks.Unsubscribe {
		stubBroker.EnqueueUnsubAck(ackKind)
	}

	var telemetryReceivers []*TestingTelemetryReceiver

	receivedTelemetries := make(chan receivedTelemetry, 10)

	for ix := range testCase.Prologue.Receivers {
		var catch *TestCaseCatch
		if ix == len(testCase.Prologue.Receivers)-1 {
			catch = testCase.Prologue.Catch
		}

		receiver := getTelemetryReceiver(
			t,
			sessionClient,
			&testCase.Prologue.Receivers[ix],
			catch,
			receivedTelemetries,
		)
		if receiver != nil {
			telemetryReceivers = append(telemetryReceivers, receiver)
		}
	}

	sourceIDs := make(map[int]string)
	packetIDs := make(map[int]uint16)

	freezeTicket := -1
	defer func() {
		if freezeTicket >= 0 {
			unfreezeTime(freezeTicket)
		}
	}()

	for _, action := range testCase.Actions {
		switch action.Kind {
		case ReceiveTelemetry:
			receiveTelemetry(
				action.AsReceiveTelemetry(),
				stubBroker,
				sourceIDs,
				packetIDs,
			)
		case AwaitAck:
			awaitAcknowledgement(t, action.AsAwaitAck(), stubBroker, packetIDs)
		case Sleep:
			sleep(action.AsSleep())
		case Disconnect:
			stubBroker.Disconnect()
		case FreezeTime:
			freezeTicket = freezeTime()
		case UnfreezeTime:
			unfreezeTime(freezeTicket)
			freezeTicket = -1
		}
	}

	for _, topic := range testCase.Epilogue.SubscribedTopics {
		require.True(t, stubBroker.HasSubscribed(topic))
	}

	if testCase.Epilogue.AcknowledgementCount != nil {
		require.Equal(
			t,
			*testCase.Epilogue.AcknowledgementCount,
			stubBroker.AcknowledgementCount,
		)
	}

	if testCase.Epilogue.TelemetryCount != nil {
		require.Equal(
			t,
			*testCase.Epilogue.TelemetryCount,
			telemetryReceivers[0].telemetryCount,
		)
	}

	for ix, telemReceiver := range telemetryReceivers {
		if exeCount, ok := testCase.Epilogue.TelemetryCounts[ix]; ok {
			require.Equal(t, exeCount, telemReceiver.telemetryCount)
		}
	}

	for _, telem := range testCase.Epilogue.ReceivedTelemetries {
		checkReceivedTelemetry(t, telem, receivedTelemetries, sourceIDs)
	}
}

func getTelemetryReceiver(
	t *testing.T,
	sessionClient protocol.MqttClient,
	tcr *TestCaseReceiver,
	catch *TestCaseCatch,
	receivedTelemetries chan receivedTelemetry,
) *TestingTelemetryReceiver {
	options := []protocol.TelemetryReceiverOption{
		protocol.WithTopicTokens(tcr.TopicTokenMap),
	}

	if tcr.TopicNamespace != nil {
		options = append(
			options,
			protocol.WithTopicNamespace(*tcr.TopicNamespace),
		)
	}

	receiver, err := NewTestingTelemetryReceiver(
		sessionClient,
		&tcr.Serializer,
		tcr.TelemetryTopic,
		func(
			_ context.Context,
			msg *protocol.TelemetryMessage[string],
		) error {
			return processTelemetry(msg, tcr, receivedTelemetries)
		},
		options...)

	if err == nil {
		err = receiver.base.Start(context.Background())
	}

	if catch == nil {
		require.NoErrorf(
			t,
			err,
			"Unexpected error returned when initializing TelemetryReceiver: %s",
			err,
		)
	} else {
		require.Errorf(t, err, "Expected %s error, but no error returned when initializing TelemetryReceiver", catch.ErrorKind)
		CheckError(t, *catch, err)
	}

	return receiver
}

func receiveTelemetry(
	actionReceiveTelemetry *TestCaseActionReceiveTelemetry,
	stubBroker *StubBroker,
	sourceIDs map[int]string,
	packetIDs map[int]uint16,
) {
	var props packets.Properties

	if actionReceiveTelemetry.SourceIndex != nil {
		sourceID, ok := sourceIDs[*actionReceiveTelemetry.SourceIndex]
		if !ok {
			guid, _ := uuid.NewV7()
			sourceID = guid.String()
			sourceIDs[*actionReceiveTelemetry.SourceIndex] = sourceID
		}
		props.User = append(props.User, packets.User{
			Key:   SourceID,
			Value: sourceID,
		})
	}

	var packetID uint16
	if actionReceiveTelemetry.PacketIndex != nil {
		var ok bool
		packetID, ok = packetIDs[*actionReceiveTelemetry.PacketIndex]
		if !ok {
			packetID = stubBroker.GetNewPacketID()
		}
	} else {
		packetID = stubBroker.GetNewPacketID()
	}

	if actionReceiveTelemetry.ContentType != nil {
		props.ContentType = *actionReceiveTelemetry.ContentType
	}

	if actionReceiveTelemetry.FormatIndicator != nil {
		payloadFormat := byte(*actionReceiveTelemetry.FormatIndicator)
		props.PayloadFormat = &payloadFormat
	}

	var payload []byte
	if actionReceiveTelemetry.Payload != nil {
		payload = []byte(*actionReceiveTelemetry.Payload)
	}

	if actionReceiveTelemetry.MessageExpiry != nil {
		messageExpiry := uint32(
			actionReceiveTelemetry.MessageExpiry.ToDuration().Seconds(),
		)
		props.MessageExpiry = &messageExpiry
	}

	for key, val := range actionReceiveTelemetry.Metadata {
		props.User = append(props.User, packets.User{
			Key:   key,
			Value: val,
		})
	}

	telemetry := packets.Publish{
		PacketID:   packetID,
		Topic:      *actionReceiveTelemetry.Topic,
		Properties: &props,
		Payload:    payload,
	}

	if actionReceiveTelemetry.Qos != nil {
		telemetry.QoS = byte(*actionReceiveTelemetry.Qos)
	}

	stubBroker.ReceiveMessage(&telemetry)

	if actionReceiveTelemetry.PacketIndex != nil {
		packetIDs[*actionReceiveTelemetry.PacketIndex] = packetID
	}
}

func checkReceivedTelemetry(
	t *testing.T,
	telem TestCaseReceivedTelemetry,
	receivedTelemetries chan receivedTelemetry,
	sourceIDs map[int]string,
) {
	rcvTelem := <-receivedTelemetries

	if telem.TelemetryValue == nil {
		require.Empty(t, rcvTelem.TelemetryValue)
	} else if val, ok := telem.TelemetryValue.(string); ok {
		require.Equal(t, val, rcvTelem.TelemetryValue)
	}

	if telem.Metadata != nil {
		for key, val := range *telem.Metadata {
			propVal, ok := rcvTelem.Metadata[key]
			require.True(t, ok)
			require.Equal(t, val, propVal)
		}
	}

	if telem.Capsule != nil {
		if telem.Capsule.CloudEvent == nil {
			require.Nil(t, rcvTelem.CloudEvent)
		} else {
			require.NotNil(t, rcvTelem.CloudEvent)

			if telem.Capsule.CloudEvent.Source != nil {
				require.Equal(
					t,
					*telem.Capsule.CloudEvent.Source,
					rcvTelem.CloudEvent.Source.String(),
				)
			}

			if telem.Capsule.CloudEvent.Type != nil {
				require.Equal(t, *telem.Capsule.CloudEvent.Type, rcvTelem.CloudEvent.Type)
			}

			if telem.Capsule.CloudEvent.ID != nil {
				require.Equal(t, *telem.Capsule.CloudEvent.ID, rcvTelem.CloudEvent.ID)
			}

			if telem.Capsule.CloudEvent.Time == nil {
				require.True(t, rcvTelem.CloudEvent.Time.IsZero())
			} else if eventTime, ok := telem.Capsule.CloudEvent.Time.(string); ok {
				require.Equal(t, eventTime, rcvTelem.CloudEvent.Time.Format(time.RFC3339))
			}

			if telem.Capsule.CloudEvent.SpecVersion != nil {
				require.Equal(
					t,
					*telem.Capsule.CloudEvent.SpecVersion,
					rcvTelem.CloudEvent.SpecVersion,
				)
			}

			if telem.Capsule.CloudEvent.DataContentType != nil {
				require.Equal(
					t,
					*telem.Capsule.CloudEvent.DataContentType,
					rcvTelem.CloudEvent.DataContentType,
				)
			}

			if telem.Capsule.CloudEvent.Subject == nil {
				require.Empty(t, rcvTelem.CloudEvent.Subject)
			} else if subject, ok := telem.Capsule.CloudEvent.Subject.(string); ok {
				require.Equal(
					t,
					subject,
					rcvTelem.CloudEvent.Subject,
				)
			}

			if telem.Capsule.CloudEvent.DataSchema == nil {
				require.Empty(t, rcvTelem.CloudEvent.DataSchema)
			} else if dataSchema, ok := telem.Capsule.CloudEvent.DataSchema.(string); ok {
				require.NotNil(t, rcvTelem.CloudEvent.DataSchema)
				require.Equal(
					t,
					dataSchema,
					rcvTelem.CloudEvent.DataSchema.String(),
				)
			}
		}
	}

	if telem.SourceIndex != nil {
		sourceID, ok := sourceIDs[*telem.SourceIndex]
		require.True(t, ok)
		require.Equal(t, sourceID, rcvTelem.SourceID)
	}
}

func processTelemetry(
	msg *protocol.TelemetryMessage[string],
	tcr *TestCaseReceiver,
	receivedTelemetries chan receivedTelemetry,
) error {
	if tcr.RaiseError.Kind == Content {
		var message string
		if tcr.RaiseError.Message != nil {
			message = *tcr.RaiseError.Message
		}
		var propertyName string
		if tcr.RaiseError.PropertyName != nil {
			propertyName = *tcr.RaiseError.PropertyName
		}
		var propertyValue string
		if tcr.RaiseError.PropertyValue != nil {
			propertyValue = *tcr.RaiseError.PropertyValue
		}
		return protocol.InvocationError{
			Message:       message,
			PropertyName:  propertyName,
			PropertyValue: propertyValue,
		}
	} else if tcr.RaiseError.Kind == Execution {
		return errors.New(*tcr.RaiseError.Message)
	}

	// Used for all telemetry, so ignore the error.
	cloudEvent, _ := protocol.CloudEventFromTelemetry(msg)

	receivedTelemetries <- receivedTelemetry{
		TelemetryValue: msg.Message.Payload,
		Metadata:       msg.Message.Metadata,
		CloudEvent:     cloudEvent,
		SourceID:       msg.ClientID,
	}

	return nil
}

type receivedTelemetry struct {
	TelemetryValue string
	Metadata       map[string]string
	CloudEvent     *protocol.CloudEvent
	SourceID       string
}
