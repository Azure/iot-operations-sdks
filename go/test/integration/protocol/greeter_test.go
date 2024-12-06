package protocol

import (
    "context"
    "fmt"
    "testing"

    "github.com/Azure/iot-operations-sdks/go/protocol"
    "github.com/Azure/iot-operations-sdks/go/samples/protocol/greeter/envoy"
    "github.com/stretchr/testify/require"
)

func TestSayHello(t *testing.T) {
    ctx := context.Background()

    client, server, done := sessionClients(t)
    defer done()

    var listeners protocol.Listeners
    defer listeners.Close()

    encReq := protocol.JSON[envoy.HelloRequest]{}
    encRes := protocol.JSON[envoy.HelloResponse]{}
    topic := "prefix/{ex:token}/suffix"

    executor, err := protocol.NewCommandExecutor(server, encReq, encRes, topic,
        func(
            _ context.Context,
            cr *protocol.CommandRequest[envoy.HelloRequest],
        ) (*protocol.CommandResponse[envoy.HelloResponse], error) {
            fmt.Printf("--> Executing Greeter.SayHello with id %s for %s\n", cr.CorrelationData, cr.ClientID)
            response := envoy.HelloResponse{
                Message: "Hello " + cr.Payload.Name,
            }
            fmt.Printf("--> Executed Greeter.SayHello with id %s for %s\n", cr.CorrelationData, cr.ClientID)
            return protocol.Respond(response, protocol.WithMetadata(cr.TopicTokens))
        },
        protocol.WithTopicNamespace("ns"),
    )
    require.NoError(t, err)
    listeners = append(listeners, executor)

    invoker, err := protocol.NewCommandInvoker(client, encReq, encRes, topic,
        protocol.WithResponseTopicSuffix("response/{executorId}"),
        protocol.WithTopicNamespace("ns"),
        protocol.WithTopicTokens{"token": "test"},
        protocol.WithTopicTokenNamespace("ex:"),
    )
    require.NoError(t, err)
    listeners = append(listeners, invoker)

    err = listeners.Start(ctx)
    require.NoError(t, err)

    req := envoy.HelloRequest{Name: "User"}
    res, err := invoker.Invoke(ctx, req,
        protocol.WithTopicTokens{"executorId": server.ID()},
    )
    require.NoError(t, err)

    expected := "Hello " + req.Name
    require.Equal(t, expected, res.Payload.Message)
}