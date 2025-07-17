# ADR 25: RPC Streaming

## Context

Users have expressed a desire to allow more than one response per RPC invocation. This would enable scenarios like:

- Execute long-running commands while still being responsive
- 

## Requirements
 - Allow for an arbitrary number of command responses

## Non-requirements
 - Different payloads per command response
 - Allow for a separate command executor to "take over" execution of a command mid-stream
 - ???? Allow command executor to determine mid stream how many responses are needed? Or should the first response outline exactly how many responses?

## Decision

RPC response includes a "is streaming and there is at least one more response" flag. If that flag isn't present in the initial RPC response, then the invoker can assume it is not streaming.

How to announce from invoker side that it supports streaming? 
 - Can't do protocol version unless all SDKs are at par. 
   - Protocol exists regardless of SDK support though, so protocol version 2.0 could just mandate streaming support
    - We need a document that tracks what each protocol version supports

    

## Sample model

## Questions

- Do we need the invoker to announce in its request that it supports streaming responses?
  - Yes. Otherwise, a legacy invoker may receive the first of a stream of requests and ignore the others. Would rather throw an exception along the lines of "Cannot invoke this command because it requires streaming support"
- Do we maintain separate APIs for invoking a streaming method vs non-streaming?
    - return Task<RpcResponse> vs IAsyncEnumerable<RpcResponse> seems useful

## Error cases

 - RPC executor dies after sending X out of Y responses. Just time out waiting on X+1'th reply?
 - RPC executor doesn't support streaming but receives a streaming request
 - RPC invoker tries to invoke a command that the executor requires streaming on
 - timeout per response vs overall?
 