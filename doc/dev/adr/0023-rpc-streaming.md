# ADR 22: RPC Streaming

## Context


## Decision

## Sample model

## Error cases

 - RPC executor dies after sending X out of Y responses. Just time out waiting on X+1'th reply?
 - RPC executor doesn't support streaming but receives a streaming request
 - timeout per response vs overall?