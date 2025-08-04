# ADR 25: RPC Streaming

## Context

Users have expressed a desire to allow more than one request and/or more than one response per RPC invocation. This would enable scenarios like:

- Execute long-running commands while still being responsive
- Allow users to report status over time for a long-running command
- Invoking a command where the executor wants a series of data points that would be impractical to put in one message

## Requirements

 - Allow for an arbitrary number of command requests and responses for a single command invocation
   - The total number of requests and responses does not need to be known before the first request/response is sent 
   - The total number of entries in a stream is allowed to be 1
 - When exposed to the user, each request and response includes an index of where it was in the stream
 - Allow for multiple separate commands to be streamed simultaneously
 - Allow for invoker and/or executor to cancel a streamed request and/or streamed response at any time

## Non-requirements

 - Different payload shapes per command response 
 - The API of the receiving side of a stream will provide the user the streamed requests/responses in their **intended** order rather than their **received** order
   - If the stream's Nth message is lost due to message expiry (or other circumstances), our API should still notify the user when the N+1th stream message is received 
   - This may be added as a feature later if requested by customers

## State of the art

gRPC supports these patterns for RPC:
- [Unary RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#unary-rpc) (1 request message, 1 response message)
- [Server streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#server-streaming-rpc) (1 request message, many response messages)
- [Client streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#server-streaming-rpc) (many request messages, one response message)
- [Bi-directional streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#bidirectional-streaming-rpc) (many request messages, many response messages)

[gRPC also allows for either the client or server to cancel an RPC at any time](https://grpc.io/docs/what-is-grpc/core-concepts/#cancelling-an-rpc)

## Decision

### API design, .NET

While RPC streaming shares a lot of similarities to normal RPC, we will define a new communication pattern to handle this scenario with two corresponding base classes: ```StreamingCommandInvoker``` and ```StreamingCommandExecutor```. 

These new base classes will use extended versions of the ```ExtendedRequest``` and ```ExtendedResponse``` classes to include the streaming-specific information about each request and response:

```csharp
public class StreamingExtendedRequest<TResp> : ExtendedRequest<TResp>
    where TResp : class
{
    /// <summary>
    /// The index of this request relative to the other requests in this request stream. Starts at 0.
    /// </summary>
    public int StreamingRequestIndex { get; set; }

    /// <summary>
    /// If true, this request is the final request in this request stream.
    /// </summary>
    public bool IsLastRequest { get; set; }
}

public class StreamingExtendedResponse<TResp> : ExtendedResponse<TResp>
    where TResp : class
{
    /// <summary>
    /// The index of this response relative to the other responses in this response stream. Starts at 0.
    /// </summary>
    public int StreamingResponseIndex { get; set; }

    /// <summary>
    /// If true, this response is the final response in this response stream.
    /// </summary>
    public bool IsLastResponse { get; set; }
}
```

#### Invoker side

The new ```StreamingCommandInvoker``` will largely look like the existing ```CommandInvoker```, but will instead have an API for ```InvokeCommandWithStreaming```.

This new method will take the same parameters as ```InvokeCommand``` but will accept a stream of requests and return a stream of command responses. 

```csharp
public abstract class StreamingCommandInvoker<TReq, TResp>
    where TReq : class
    where TResp : class
{
    // Many requests, many responses.
    public IAsyncEnumerable<StreamingExtendedResponse<TResp>> InvokeStreamingCommandAsync(IAsyncEnumerable<StreamingExtendedRequest<TReq>> requests, ...) {...}
}
```

#### Executor side

The new ```StreamingCommandExecutor``` will largely look like the existing ```CommandExecutor```, but the callback to notify users that a command was received will include a stream of requests and return a stream of responses.

```csharp
public abstract class StreamingCommandExecutor<TReq, TResp> : IAsyncDisposable
    where TReq : class
    where TResp : class
{
    /// <summary>
    /// A streaming command was invoked
    /// </summary>
    /// <remarks>
    /// The callback provides the stream of requests and requires the user to return one to many responses.
    /// </remarks>
    public required Func<IAsyncEnumerable<StreamingExtendedRequest<TReq>>, CancellationToken, Task<IAsyncEnumerable<StreamingExtendedResponse<TResp>>>> OnStreamingCommandReceived { get; set; }
}

```

With this design, commands that use streaming are defined at codegen time. Codegen layer changes will be defined in a separate ADR, though.

### MQTT layer protocol

#### Streaming user property

To convey streaming context in a request/response stream, we will put this information in the "__stream" MQTT user property with a value that looks like:

```<index>_<isLast>_<cancelRequest>```

with data types

```<uint>_<boolean>_<boolean>```

examples:

```0_false_false```: The first (and not last) message in a stream

```3_true_false```: The third and final message in a stream

```0_true_false```: The first and final message in a stream

```0_true_true```: This stream should be canceled. Note that the values for ```index```, and ```isLast``` are ignored here.

[see cancellation support for more details on cancellation scenarios](#cancellation-support)

#### Invoker side

The streaming command invoker will first subscribe to the appropriate response topic prior to sending any requests

Once the user invokes a streaming command, the streaming command invoker will send one to many MQTT messages with:
  - The same response topic
  - The same correlation data
  - The appropriate streaming metadata [see above](#streaming-user-property)
  - The serialized payload as provided by the user's request object
  - Any user-definied metadata as specified in the ```ExtendedRequest```

Once the stream of requests has finished sending, the streaming command invoker should expect the stream of responses to arrive on the provided response topic with the provided correlation data and the streaming user property.

The command invoker will acknowledge all messages it receives that match the correlation data of a known streaming command

#### Executor side

A streaming command executor should start by subscribing to the expected command topic
  - Even though the streaming command classes are separate from the existing RPC classes, they should also offer the same features around topic string pre/suffixing, custom topic token support, etc.

Upon receiving a MQTT message that contains a streaming request, the streaming executor should notify the application layer that the first message in a request stream was received. Once the executor has notified the user that the final message in a request stream was received, the user should be able to provide a stream of responses. Upon receiving each response in that stream from the user, the executor will send an MQTT message for each streamed response with:
  - The same correlation data as the original request
  - The topic as specified by the original request's response topic field
  - The appropriate streaming metadata [see above](#streaming-user-property)
  - The serialized payload as provided by the user's response object
  - Any user-definied metadata as specified in the ```ExtendedResponse```

Unlike normal RPC, the streaming command executor will not provide any cache support. This is because streams may grow indefinitely in length and size.

Also unlike normal RPC, the stream command executor should acknowledge the MQTT message of a received stream request as soon as the user has been notified about it. We cannot defer acknowledging the stream request messages until after the full command has finished as streams may run indefinitely and we don't want to block other users of the MQTT client.

### Timeout support

We need to provide timeout support for our streaming APIs to avoid scenarios such as:

- The invoker side is stuck waiting for the final response in a stream because it was lost or the executor side crashed before sending it.
- The executor side is stuck waiting for the final request in a stream because it was lost or the invoker side crashed before sending it.
- The broker delivers a request/response stream message that is "no longer relevant"

#### Decision

TODO

Invoker side:
 - delivery timeout (assigned per request message in stream (extended streaming request assigned?))
 - execution timeout (noted in header of each request (for redundancy in case first message is lost))
 - overall timeout (not sent over the wire, just the amount of time the invoker should wait from API call to final response before giving up)

 Executor side:
  - At constructor time, user assigns 


#### Alternative timeout designs considered

- Include in the initial request user properties the number of milliseconds allowed between receiving the final command request and delivering the final command response.
  - This is the approach that gRPC takes, but... 
    - It doesn't account well for delays in message delivery from broker. 
    - It doesn't account for scenarios where the invoker/executor dies unexpectedly (since gRPC relies on a direct connection between invoker and executor)
- Allow users to specify timeouts for delivery and a separate timeout for execution
  - a bit complex on API surface. Also subtly different enough from how our normal RPC does timeouts that it would cause confusion

### Cancellation support

To avoid scenarios where long-running streaming requests/responses are no longer wanted, we will want to support cancelling streaming RPC calls. 

Since sending a cancellation request may fail (message expiry on broker side), the SDK API design should allow for the user to repeatedly call "cancel" and should return successfully once the other party has responded appropriately. 

The proposed design for that would look like:

```csharp

public abstract class StreamingCommandInvoker<TReq, TResp>
    where TReq : class
    where TResp : class
{
    public async Task CancelStreamingCommandAsync(Guid correlationId) {...}
}

public abstract class StreamingCommandExecutor<TReq, TResp> : IAsyncDisposable
    where TReq : class
    where TResp : class
{
    public async Task CancelStreamingCommandAsync(Guid correlationId) {...}
}

```

where the user gets the correlationId from the ```CommandRequestMetadata``` they provide to the command invoker when invoking a command or the ```CommandResponseMetadata``` that the executor gives them upon receiving a streaming command.

#### Invoker side

- The command invoker may cancel a streaming command while streaming the request or receiving the stream of responses by sending an MQTT message with: 
  - The same MQTT topic as the invoked method
  - The same correlation data as the invoked method 
  - Streaming metadata with the ["cancel" flag set](#streaming-user-property)
  - No payload
- The command invoker should still listen on the response topic for a response from the executor which may still contain a successful response (if cancellation was received after the command completed successfully) or a response signalling that cancellation succeeded ("Canceled" error code)

As detailed below, the executor may also cancel the stream at any time. In response to receiving a cancellation request from the executor, the invoker should send an MQTT message with:
 - The same topic as the command itself
 - The same correlation data as the command itself
 - The "Canceled" error code

Any received MQTT messages pertaining to a command that was already canceled should still be acknowledged. They should not be given to the user, though.

#### Executor side

Upon receiving an MQTT message with the stream "cancel" flag set to "true" that correlates to an actively executing streaming command, the command executor should:
 - Notify the application layer that that RPC has been canceled if it is still running
 - Send an MQTT message to the appropriate response topic with error code "canceled" to notify the invoker that the RPC has stopped and no further responses will be sent.

If the executor receives a cancellation request for a streaming command that has already completed, then the cancellation request should be ignored.

The executor may cancel receiving a stream of requests or cancel sending a stream of responses as well. It does so by sending an MQTT message to the invoker with:
  - The same MQTT topic as command response
  - The same correlation data as the invoked method 
  - Streaming metadata with the ["cancel" flag set](#streaming-user-property)
  - No payload

The command invoker should then send a message on the same command topic with the same correlation data with the "stream canceled successfully" flag set.

Any received MQTT messages pertaining to a command that was already canceled should still be acknowledged. They should not be given to the user, though.

### Protocol versioning

By maintaining RPC streaming as a separate communication pattern from normal RPC, we will need to introduce an independent protocol version for RPC streaming specifically. It will start at ```1.0``` and should follow the same protocol versioning rules as the protocol versions used by telemetry and normal RPC.

## Alternative designs considered

 - Allow the command executor to decide at run time of each command if it will stream responses independent of the command invoker's request
   - This would force users to always call the ```InvokeCommandWithStreaming``` API on the command invoker side and that returned object isn't as easy to use for single responses
 - Treat streaming RPC as the same protocol as RPC
   - This introduces a handful of error cases such as:
     - Invoker invokes a method that it thinks is non-streaming, but the executor tries streaming responses
     - Executor receives a streaming command but the user did not set the streaming command handler callback (which must be optional since not every command executor has streaming commands)
   - API design is messy because a command invoker/executor should not expose streaming command APIs if they have no streaming commands
   - Caching behavior of normal RPC doesn't fit well with streamed RPCs which may grow indefinitely large
