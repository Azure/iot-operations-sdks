// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
package protocol

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"os"
	"path/filepath"
	"slices"
	"strings"
	"testing"

	"github.com/Azure/iot-operations-sdks/go/protocol"
	"github.com/BurntSushi/toml"
	"github.com/eclipse/paho.golang/paho"
	"github.com/stretchr/testify/require"
	"gopkg.in/yaml.v3"
)

func RunCommandInvokerTests(t *testing.T) {
	var commandInvokerDefaultInfo DefaultTestCase

	_, err := toml.DecodeFile(
		"../../../eng/test/test-cases/Protocol/CommandInvoker/defaults.toml",
		&commandInvokerDefaultInfo,
	)
	if err != nil {
		panic(err)
	}

	TestCaseDefaultInfo = &commandInvokerDefaultInfo

	files, err := filepath.Glob(
		"../../../eng/test/test-cases/Protocol/CommandInvoker/*.yaml",
	)
	if err != nil {
		log.Fatal(err)
	}

	enableFreezing()

	for ix, f := range files {
		testName, _ := strings.CutSuffix(filepath.Base(f), ".yaml")
		t.Run(testName, func(t *testing.T) {
			runOneCommandInvokerTest(t, ix, testName, f)
		})
	}
}

func runOneCommandInvokerTest(
	t *testing.T,
	testCaseIndex int,
	testName string,
	fileName string,
) {
	pendingTestCases := []string{}

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
		mqttClientID = fmt.Sprintf("InvokerTestClient%d", testCaseIndex)
	}

	stubClient, sessionClient := getStubAndSessionClient(t, mqttClientID)

	for _, ackKind := range testCase.Prologue.PushAcks.Publish {
		stubClient.enqueuePubAck(ackKind)
	}

	for _, ackKind := range testCase.Prologue.PushAcks.Subscribe {
		stubClient.enqueueSubAck(ackKind)
	}

	for _, ackKind := range testCase.Prologue.PushAcks.Unsubscribe {
		stubClient.enqueueUnsubAck(ackKind)
	}

	commandInvokers := make(map[string]*TestingCommandInvoker)

	for ix, tci := range testCase.Prologue.Invokers {
		var catch *TestCaseCatch
		if ix == len(testCase.Prologue.Invokers)-1 {
			catch = testCase.Prologue.Catch
		}

		invoker := getCommandInvoker(t, sessionClient, tci, catch)
		if invoker != nil {
			commandInvokers[*tci.CommandName] = invoker
		}
	}

	invocationChans := make(map[int]chan ExtendedResponse)
	correlationIDs := make(map[int][]byte)
	packetIDs := make(map[int]uint16)

	freezeTicket := -1
	defer func() {
		if freezeTicket >= 0 {
			unfreezeTime(freezeTicket)
		}
	}()

	for _, action := range testCase.Actions {
		switch action.Kind {
		case InvokeCommand:
			invokeCommand(
				t,
				action.AsInvokeCommand(),
				commandInvokers,
				invocationChans,
			)
		case AwaitInvocation:
			awaitInvocation(t, action.AsAwaitInvocation(), invocationChans)
		case ReceiveResponse:
			receiveResponse(
				t,
				action.AsReceiveResponse(),
				stubClient,
				correlationIDs,
				packetIDs,
			)
		case AwaitAck:
			awaitAcknowledgement(t, action.AsAwaitAck(), stubClient, packetIDs)
		case AwaitPublish:
			awaitPublish(
				t,
				action.AsAwaitPublish(),
				stubClient,
				correlationIDs,
			)
		case Sleep:
			sleep(action.AsSleep())
		case Disconnect:
			t.Skipf(
				"Skipping test %s because action 'disconnect' not implemented",
				testName,
			)
		case FreezeTime:
			freezeTicket = freezeTime()
		case UnfreezeTime:
			unfreezeTime(freezeTicket)
			freezeTicket = -1
		}
	}

	for _, topic := range testCase.Epilogue.SubscribedTopics {
		require.True(t, stubClient.hasSubscribed(topic))
	}

	if testCase.Epilogue.PublicationCount != nil {
		require.Equal(
			t,
			*testCase.Epilogue.PublicationCount,
			stubClient.PublicationCount(),
		)
	}

	for _, publishedMessage := range testCase.Epilogue.PublishedMessages {
		checkPublishedRequest(t, publishedMessage, stubClient, correlationIDs)
	}

	if testCase.Epilogue.AcknowledgementCount != nil {
		require.Equal(
			t,
			*testCase.Epilogue.AcknowledgementCount,
			stubClient.AcknowledgementCount(),
		)
	}
}

func getCommandInvoker(
	t *testing.T,
	sessionClient protocol.MqttClient,
	tci TestCaseInvoker,
	catch *TestCaseCatch,
) *TestingCommandInvoker {
	options := []protocol.CommandInvokerOption{
		protocol.WithTopicTokens(tci.CustomTokenMap),
		protocol.WithTopicTokenNamespace("ex:"),
	}

	if tci.ResponseTopicPrefix != nil {
		options = append(
			options,
			protocol.WithResponseTopicPrefix(*tci.ResponseTopicPrefix),
		)
	}

	if tci.ResponseTopicSuffix != nil {
		options = append(
			options,
			protocol.WithResponseTopicSuffix(*tci.ResponseTopicSuffix),
		)
	}

	if tci.ResponseTopicMap != nil {
		options = append(
			options,
			protocol.WithResponseTopic(
				func(reqTopic string) string { return *(*tci.ResponseTopicMap)[reqTopic] },
			),
		)
	}

	if tci.TopicNamespace != nil {
		options = append(
			options,
			protocol.WithTopicNamespace(*tci.TopicNamespace),
		)
	}

	invoker, err := NewTestingCommandInvoker(
		sessionClient,
		tci.CommandName,
		tci.RequestTopic,
		tci.ModelID,
		options...)

	if err == nil {
		err = invoker.base.Start(context.Background())
	}

	if catch == nil {
		require.NoErrorf(
			t,
			err,
			"Unexpected error returned when initializing CommandInvoker: %s",
			err,
		)
	} else {
		if err == nil {
			_, err = invoker.base.Invoke(context.Background(), *TestCaseDefaultInfo.Actions.InvokeCommand.GetRequestValue(),
				protocol.WithTopicTokens{"executorId": *TestCaseDefaultInfo.Actions.InvokeCommand.GetExecutorID()},
				protocol.WithTopicTokens{"commandName": *tci.CommandName},
			)
		}

		require.Errorf(t, err, "Expected %s error, but no error returned when initializing CommandInvoker", catch.ErrorKind)
	}

	return invoker
}

func invokeCommand(
	_ *testing.T,
	actionInvokeCommand *TestCaseActionInvokeCommand,
	commandInvokers map[string]*TestingCommandInvoker,
	invocationChans map[int]chan ExtendedResponse,
) {
	invocationChan := make(chan ExtendedResponse)
	invocationChans[actionInvokeCommand.InvocationIndex] = invocationChan

	req := *actionInvokeCommand.RequestValue

	options := []protocol.InvokeOption{}
	options = append(
		options,
		protocol.WithMessageExpiry(
			uint32(actionInvokeCommand.Timeout.ToDuration().Seconds()),
		),
	)

	if actionInvokeCommand.ExecutorID != nil {
		options = append(
			options,
			protocol.WithTopicTokens{
				"executorId": *actionInvokeCommand.ExecutorID,
			},
		)
	}

	if actionInvokeCommand.Metadata != nil {
		for key, val := range *actionInvokeCommand.Metadata {
			options = append(
				options,
				protocol.WithMetadata{
					key: val,
				},
			)
		}
	}

	tci := commandInvokers[*actionInvokeCommand.CommandName]

	go func() {
		resp, err := tci.base.Invoke(
			context.Background(),
			req,
			options...)
		invocationChan <- ExtendedResponse{
			Response: resp,
			Error:    err,
		}
	}()
}

func awaitInvocation(
	t *testing.T,
	actionAwaitInvocation *TestCaseActionAwaitInvocation,
	invocationChans map[int]chan ExtendedResponse,
) {
	invocationChan := invocationChans[actionAwaitInvocation.InvocationIndex]
	extResp := <-invocationChan

	if actionAwaitInvocation.Catch == nil {
		require.NoErrorf(
			t,
			extResp.Error,
			"Unexpected error returned when awaiting CommandInvoker.Invoke()",
		)

		if responseValue, ok := actionAwaitInvocation.ResponseValue.(string); ok {
			require.Equal(t, responseValue, extResp.Response.Payload)
		}
	} else {
		require.Errorf(t, extResp.Error, "Expected %s error, but no error returned when awaiting CommandInvoker.Invoke()", actionAwaitInvocation.Catch.ErrorKind)
		CheckError(t, *actionAwaitInvocation.Catch, extResp.Error)
	}
}

func receiveResponse(
	t *testing.T,
	actionReceiveResponse *TestCaseActionReceiveResponse,
	stubClient StubClient,
	correlationIDs map[int][]byte,
	packetIDs map[int]uint16,
) {
	var props paho.PublishProperties

	var packetID uint16
	if actionReceiveResponse.PacketIndex != nil {
		packetID = packetIDs[*actionReceiveResponse.PacketIndex]
	} else {
		packetID = stubClient.getNewPacketID()
	}

	if actionReceiveResponse.ContentType != nil {
		props.ContentType = *actionReceiveResponse.ContentType
	}

	if actionReceiveResponse.FormatIndicator != nil {
		payloadFormat := byte(*actionReceiveResponse.FormatIndicator)
		props.PayloadFormat = &payloadFormat
	}

	var payload []byte
	if actionReceiveResponse.Payload != nil {
		if actionReceiveResponse.BypassSerialization {
			payload = []byte(*actionReceiveResponse.Payload)
		} else {
			var err error
			payload, err = json.Marshal(*actionReceiveResponse.Payload)
			require.NoErrorf(t, err, "Unexpected error serializing payload: %s", err)
		}
	}

	if actionReceiveResponse.CorrelationIndex != nil {
		props.CorrelationData = correlationIDs[*actionReceiveResponse.CorrelationIndex]
	}

	if actionReceiveResponse.MessageExpiry != nil {
		messageExpiry := uint32(
			actionReceiveResponse.MessageExpiry.ToDuration().Seconds(),
		)
		props.MessageExpiry = &messageExpiry
	}

	for key, val := range actionReceiveResponse.Metadata {
		props.User = append(props.User, paho.UserProperty{
			Key:   key,
			Value: val,
		})
	}

	if actionReceiveResponse.Status != nil {
		props.User = append(props.User, paho.UserProperty{
			Key:   Status,
			Value: *actionReceiveResponse.Status,
		})
	}

	if actionReceiveResponse.StatusMessage != nil {
		props.User = append(props.User, paho.UserProperty{
			Key:   StatusMessage,
			Value: *actionReceiveResponse.StatusMessage,
		})
	}

	if actionReceiveResponse.IsApplicationError != nil {
		props.User = append(props.User, paho.UserProperty{
			Key:   IsApplicationError,
			Value: *actionReceiveResponse.IsApplicationError,
		})
	}

	if actionReceiveResponse.InvalidPropertyName != nil {
		props.User = append(props.User, paho.UserProperty{
			Key:   InvalidPropertyName,
			Value: *actionReceiveResponse.InvalidPropertyName,
		})
	}

	if actionReceiveResponse.InvalidPropertyValue != nil {
		props.User = append(props.User, paho.UserProperty{
			Key:   InvalidPropertyValue,
			Value: *actionReceiveResponse.InvalidPropertyValue,
		})
	}

	response := paho.Publish{
		PacketID:   packetID,
		Topic:      *actionReceiveResponse.Topic,
		Properties: &props,
		Payload:    payload,
	}

	if actionReceiveResponse.Qos != nil {
		response.QoS = byte(*actionReceiveResponse.Qos)
	}

	stubClient.receiveMessage(&response)

	if actionReceiveResponse.PacketIndex != nil {
		packetIDs[*actionReceiveResponse.PacketIndex] = packetID
	}
}

func checkPublishedRequest(
	t *testing.T,
	publishedMessage TestCasePublishedMessage,
	stubClient StubClient,
	correlationIDs map[int][]byte,
) {
	var lookupKey []byte
	if publishedMessage.CorrelationIndex != nil {
		lookupKey = correlationIDs[*publishedMessage.CorrelationIndex]
	}

	msg, ok := stubClient.getPublishedMessage(lookupKey)
	require.True(t, ok)

	if publishedMessage.Topic != nil {
		require.Equal(t, *publishedMessage.Topic, msg.Topic)
	}

	if publishedMessage.Payload == nil {
		require.Empty(t, msg.Payload)
	} else if payload, ok := publishedMessage.Payload.(string); ok {
		payloadBytes, err := json.Marshal(payload)
		require.NoError(t, err)
		require.Equal(t, payloadBytes, msg.Payload)
	}

	for key, val := range publishedMessage.Metadata {
		propVal, ok := getUserProperty(t, msg, key)
		require.True(t, ok)
		require.Equal(t, val, propVal)
	}

	if publishedMessage.InvokerID != nil {
		invokerID, ok := getUserProperty(t, msg, InvokerClientID)
		require.True(t, ok)
		require.Equal(t, *publishedMessage.InvokerID, invokerID)
	}
}
