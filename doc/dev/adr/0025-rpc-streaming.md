# ADR 25: RPC Streaming

## Context

Users have expressed a desire to allow more than one response per RPC invocation. This would enable scenarios like:

- Execute long-running commands while still being responsive
- 

## Requirements
 - Allow for an arbitrary number of command responses for a single command invocation
   - The total number of responses does not need to be known before the first response is sent 
 - When exposed to the user, each response includes an index of where it was in the stream
 - Allow for multiple separate commands to be streamed simultaneously
   - Even the same command can be executed in parallel to itself?

## Non-requirements
 - Different payload shapes per command response 

## State of the art

What does gRPC do?

## Decision

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

## Example with code gen

TODO which existing client works well for long-running commands? Mem mon ("Report usage for 10 seconds at 1 second intervals")?

### MQTT layer implementation

#### Command invoker side

- The command invoker's request message will include an MQTT user property with name "__isStream" and value "true".
  - Otherwise, the request message will look the same as a non-streaming RPC request
- The command invoker will listen for command responses with the correlation data that matches the invoked method's correlation data until it receives a response with the "__isLastResp" flag
- The command invoker will acknowledge all messages it receives that match the correlation data of the command request

#### Command executor side

- All command responses will use the same MQTT message correlation data as the request provided so that the invoker can map responses to the appropriate command invocation.
- Each streamed response will contain an MQTT user property with name "__streamRespId" and value equal to that response's streaming response Id.
- The final command response will include an MQTT user property "__isLastResp" with value "true" to signal that it is the final response in the stream.
- A streaming command is allowed to have a single response. If the stream only has one response, it should include both the "__isStream" and "__isLastResp" flags set.
- All **completed** streamed command responses will be added to the command response cache
  - If we cache incompleted commands, will the cache hit just wait on cache additions to get the remaining responses?
  - Cache exists for de-duplication, and we want that even for long-running RPC, right?
    - Re-sending previous responses would potentially get picked up by the original invoker twice
      - Enforced unique stream response Ids would help de-dup on the invoker side
        - Needless traffic here though
  - Separate cache for data structure purposes?

### Protocol version update

This feature is not backwards compatible (old invoker can't communicate with new executor that may try to stream a response), so it requires a bump in our RPC protocol version from "1.0" to "2.0".

TODO: Start defining a doc in our repo that defines what features are present in what protocol version.

## Alternative designs considered

 - Allow the command executor to decide at run time of each command if it will stream responses
   - This would force users to call the ```InvokeCommandWithStreaming``` API on the command invoker side and that returned object isn't as easy to use for single responses
 - Treat streaming RPC as a separate protocol from RPC, give it its own client like ```CommandInvoker``` and ```TelemetrySender```
   - There is a lot of code re-use between RPC and streaming RPC so this would make implementation very inconvenient
   - This would introduce another protocol to version. Future RPC changes would likely be relevant to RPC streaming anyways, so this feels redundant.

## Error cases

 - RPC executor dies after sending X out of Y responses. Just time out waiting on X+1'th reply?
 - RPC executor doesn't support streaming but receives a streaming request
   - RPC executor responds with "NotSupportedVersion" error code
 - RPC invoker tries to invoke a command that the executor requires streaming on
 - timeout per response vs overall?
 
 ## Open Questions

- Do we need to include response index user property on each streamed response?
  - MQTT message ordering suggests this information can just be inferred by the command invoker
- Command timeout/cancellation tokens in single vs streaming?
- When to ack the streaming request?
  - In normal RPC, request is Ack'd only after the method finishes invocation, but this would likely clog up Acks since streaming requests can take a while.
    - Ack after first response is generated?