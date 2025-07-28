# ADR 25: RPC Streaming

## Context

Users have expressed a desire to allow more than one response per RPC invocation. This would enable scenarios like:

- Execute long-running commands while still being responsive
- Allow users to report status over time for a long-running command

## Requirements

 - Allow for an arbitrary number of command responses for a single command invocation
   - The total number of responses does not need to be known before the first response is sent 
 - When exposed to the user, each response includes an index of where it was in the stream and an optional response Id
 - Allow for multiple separate commands to be streamed simultaneously
 - Allow for invoker to cancel streamed responses mid-stream

## Non-requirements

 - Different payload shapes per command response 
 - "Client Streaming" RPC (multiples requests -> One command response)
 - Bi-directional streaming RPC (multiples requests -> multiple responses)
 - Allow for executor to cancel streamed responses mid-stream

## State of the art

gRPC supports these patterns for RPC:
- [Unary RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#unary-rpc) (1 request message, 1 response message)
- [Server streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#server-streaming-rpc) (1 request message, many response messages)
- [Client streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#server-streaming-rpc) (many request messages, one response message)
- [Bi-directional streaming RPC](https://grpc.io/docs/what-is-grpc/core-concepts/#bidirectional-streaming-rpc) (many request messages, many response messages)

[gRPC also allows for either the client or server to cancel an RPC at any time](https://grpc.io/docs/what-is-grpc/core-concepts/#cancelling-an-rpc)

## Decision

### API design, .NET

#### Invoker side

Our command invoker base class will now include a new method ```InvokeCommandWithStreaming``` to go with the existing ```InvokeCommand``` method. 

This new method will take the same parameters as ```InvokeCommand``` but will return an asynchronously iterable list (or callback depending on language?) of command response objects. 

```csharp
public abstract class CommandInvoker<TReq, TResp>
    where TReq : class
    where TResp : class
{
    // Single response
    public Task<ExtendedResponse<TResp>> InvokeCommandAsync(TReq request, ...) {...}

    // Many responses, responses may be staggered
    public IAsyncEnumerable<StreamingExtendedResponse<TResp>> InvokeStreamingCommandAsync(TReq request, ...) {...}
}
```

Additionally, this new method will return an extended version of the ```ExtendedResponse``` wrapper that will include the streaming-specific information about each response:

```csharp
public class StreamingExtendedResponse<TResp> : ExtendedResponse<TResp>
    where TResp : class
{
    /// <summary>
    /// An optional Id for this response (relative to the other responses in this response stream)
    /// </summary>
    /// <remarks>
    /// Users are allowed to provide Ids for each response, only for specific responses, or for none of the responses.
    /// </remarks>
    public string? StreamingResponseId { get; set; }

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

#### Executor side

On the executor side, we will define a separate callback that executes whenever a streaming command is invoked. Instead of returning the single response, this callback will return the asynchronously iterable list of responses. Importantly, this iterable may still be added to by the user after this callback has finished. 

```csharp
public abstract class CommandExecutor<TReq, TResp> : IAsyncDisposable
    where TReq : class
    where TResp : class
{
    /// <summary>
    /// The callback to execute each time a non-streaming command request is received.
    /// </summary>
    /// <remarks>
    /// This callback may be null if this command executor only supports commands that stream responses.
    /// </remarks>
    public Func<ExtendedRequest<TReq>, CancellationToken, Task<ExtendedResponse<TResp>>>? OnCommandReceived { get; set; }

    /// <summary>
    /// The callback to execute each time a command request that expects streamed responses is received.
    /// </summary>
    /// <remarks>
    /// The callback provides the request itself and requires the user to return one to many responses. This callback may be null
    /// if this command executors doesn't have any streaming commands.
    /// </remarks>
    public Func<ExtendedRequest<TReq>, CancellationToken, Task<IAsyncEnumerable<StreamingExtendedResponse<TResp>>>>? OnStreamingCommandReceived { get; set; }
}

```

With this design, commands that use streaming are defined at codegen time. Codegen layer changes will be defined in a separate ADR, though.

### MQTT layer protocol

#### Command invoker side

- The command invoker's request message will include an MQTT user property with name "__streamResp" and value "true".
  - Executor needs to know if it can stream the response, and this is the flag that affirms it
- The command invoker will listen for command responses with the correlation data that matches the invoked method's correlation data until it receives a response with the "__isLastResp" flag set to "true"
- The command invoker will acknowledge all messages it receives that match the correlation data of the command request

#### Command executor side

- The command executor receives a command with "__streamResp" flag set to "true"
  - All command responses will use the same MQTT message correlation data as the request provided so that the invoker can map responses to the appropriate command invocation.
  - Each streamed response must contain an MQTT user property with name "__streamIndex" and value equal to the index of this response relative to the other responses (0 for the first response, 1 for the second response, etc.)
  - Each streamed response may contain an MQTT user property with name "__streamRespId" and value equal to that response's streaming response Id. This is an optional and user-provided value.
  - The final command response will include an MQTT user property "__isLastResp" with value "true" to signal that it is the final response in the stream.
    - A streaming command is allowed to have a single response. It must include the "__isLastResp" flag in that first/final response
  - Cache is only updated once the stream has completed and it is updated to include all of the responses (in order) for the command so they can be re-played if the streaming command is invoked again by the same client

- The command executor receives a command **without** "__streamResp" flag set to "true"
  - The command must be responded to without streaming

### Cancellation support

To avoid scenarios where long-running streaming responses are no longer wanted, we will want to support cancelling streaming RPC calls.

#### Invoker side

- The command invoker may cancel a streaming RPC call at an arbitrary time by sending an MQTT message with: 
  - The same MQTT topic as the invoked method
  - The same correlation data as the invoked method 
  - The user property "__stopRpc" set to "true".
  - No payload
- The command invoker should still listen on the response topic for a response from the executor which may still contain a successful response (if cancellation was received after the command completed successfully) or a response signalling that cancellation succeeded

#### Executor side

Upon receiving an MQTT message with the "__stopRpc" flag set to "true" that correlates to an actively executing streaming command, the command executor should:
 - Notify the application layer that that RPC has been canceled if it is still running
 - Send an MQTT message to the appropriate response topic with error code "canceled" to notify the invoker that the RPC has stopped and no further responses will be sent.

If the executor receives a cancellation request for a streaming command that has already completed, then the cancellation request should be ignored.

### Protocol version update

This RPC streaming feature is not backwards compatible (new invoker can't initiate what it believes is a streaming RPC call on an old executor), so it requires a bump in our RPC protocol version from "1.0" to "2.0".

## Alternative designs considered

 - Allow the command executor to decide at run time of each command if it will stream responses independent of the command invoker's request
   - This would force users to always call the ```InvokeCommandWithStreaming``` API on the command invoker side and that returned object isn't as easy to use for single responses
 - Treat streaming RPC as a separate protocol from RPC, give it its own client like ```CommandInvoker``` and ```TelemetrySender```
   - There is a lot of code re-use between RPC and streaming RPC so this would make implementation very inconvenient
   - This would introduce another protocol to version. Future RPC changes would likely be relevant to RPC streaming anyways, so this feels redundant.

## Error cases

 - RPC executor dies before sending the final stream response. 
   - Command invoker throws time out exception waiting on the next response
 - RPC executor receives command request with "__streamResp", but that executor doesn't understand streaming requests because it uses an older protocol version
   - Command executor responds with "not supported protocol" error code since the request carried protocol version 2.0
 - RPC executor receives command request with "__streamResp", and the executor understands that it is a streaming request (protocol versions align) but that particular command doesn't support streaming
   - RPC executor treats it like a non-streaming command, but adds the "__isLastResp" flag to the one and only response
 - RPC invoker tries to invoke a non-streaming command that the executor requires streaming on
   - Atypical case since codegen will prevent this
   - But, for the sake of non-codegen users, executor returns "invalid header" error pointing to the "__streamResp" header
     - Invoker understands that, if the "invalid header" value is "__streamResp", it attempted a invoke a streaming method
 
 ## Open Questions

- When to ack the streaming request?
  - In normal RPC, request is Ack'd only after the method finishes invocation. Waiting until a streamed RPC finishes could clog up Acks since streaming requests can take a while.
    - Ack after first response is generated?