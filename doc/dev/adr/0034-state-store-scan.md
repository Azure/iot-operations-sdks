# ADR 34: State Store Filter Keys API

## Context

Broker ADR 0092, "DSS Scan and MGET" , defines the State Store
`SCAN` protocol and selects cursor-based pagination. A response contains one
page of matching keys and, unless the scan is complete, a continuation token
for the next request.

This ADR decides how the SDK exposes the paginated operation to applications.

The SDK must decide:

1. whether to fetch all pages eagerly or fetch pages only when requested;
2. which pagination pattern to expose; and
3. whether continuation tokens are part of the public API.

## Options considered

### Result delivery

| Option | Assessment |
| --- | --- |
| Return all matching keys | One call returns the complete result, but accumulates it in memory and prevents early processing or exit |
| Return one broker page per application request | Keeps memory bounded and lets the application process, pause, or stop after any page |
---

### Pagination mechanism

| Option | Assessment |
| --- | --- |
| External stream | Adds the `futures` dependency and may require applications to import `futures::StreamExt` and pin the stream |
| Async iterator | 1. `std::async_iter::AsyncIterator` requires the SDK and application to compile with nightly Rust using `#![feature(async_iterator)]`.<br>2. The `async-stream` crate generates a `futures::Stream`, not a standard async iterator. Applications generally need the `futures` crate, `futures::StreamExt`, and stream pinning to consume it. |
| SDK-owned async `next()` | Implements paging directly with one small SDK type |
---

### Continuation-token exposure

Exposing the token would allow an application to resume after a restart.
However, the application must save the token after every page and handle pages
being processed more than once if a crash occurs. Exposing the token would
also add public API that must be maintained.

Keeping the token internal still supports lazy paging. Token access can be
added later without breaking existing applications, but cannot be removed
after becoming public. Without token access, a filter-keys operation restarts
after an application restart.

The `redis-rs` crate exposes async SCAN through
[`AsyncCommands::scan`](https://docs.rs/redis/latest/redis/trait.AsyncCommands.html#method.scan)
and
[`AsyncCommands::scan_match`](https://docs.rs/redis/latest/redis/trait.AsyncCommands.html#method.scan_match).
The
[`redis-rs` async iterator](https://docs.rs/redis/latest/redis/struct.AsyncIter.html)
also keeps its SCAN cursor internal: its public API exposes item retrieval and
the `Stream` implementation, but no cursor accessor. Its
[source](https://docs.rs/redis/latest/src/redis/cmd.rs.html#181-270) stores the
cursor in private iterator state.

## Decision

### Fetch pages lazily

The SDK returns a caller-driven pagination object. Each async `next()` call
requests one page and uses the previous response token. It returns `None` when
the broker does not return a continuation token.

Conceptually:

```rust
pub struct FilterKeysPager<'client> {
    // Private pagination state.
}

impl Client {
    pub fn filter_keys<'client>(
        &'client self,
        pattern: Vec<u8>,
        timeout: Duration,
    ) -> Result<FilterKeysPager<'client>, Error> {
        // ...
    }
}

impl<'client> FilterKeysPager<'client> {
    pub async fn next(&mut self) -> Result<Option<Vec<Vec<u8>>>, Error> {
        // ...
    }
}

let mut pager = client.filter_keys(pattern, timeout)?;

while let Some(keys) = pager.next().await? {
     // Process this page of keys.
}
```

This keeps one page in memory and makes requests only when the application
calls `next()`. The application may stop when its processing condition is met,
without requesting the remaining pages.

### Use an SDK-owned async `next()` pattern

The pagination object provides an inherent async `next()` method. It does not
implement a stream or async-iterator trait.

SCAN requests are serial because the SDK cannot construct the next request
until the broker returns its continuation token in the current response. An
inherent async `next()` method supports this directly, without an external
stream or async-iterator dependency.

Implementing `Stream` would also opt this API into the broader `StreamExt`
combinator model, including operations such as `filter` and `map`. These
capabilities do not add much value to this operation because each response
already contains only keys matching the requested pattern, and
continuation-token dependencies require page requests to remain serial. An
inherent async `next()` keeps the API focused on the required page-at-a-time
behavior.

The object keeps the client reference, pattern, timeout, current token, and
completion state. The pager is valid only while the State Store client remains
alive.

### Keep continuation tokens internal

The pagination object keeps and forwards the continuation token internally. It
does not expose the token. After an application restart, a filter-keys
operation starts again from the beginning. This is accepted because the API is
intended to provide bounded, page-at-a-time processing. Applications that
restart may process matching keys again, while the SDK avoids exposing
persistence and recovery semantics that could be added later without breaking
the initial API.

## Consequences

- Applications process results page by page.
- Empty pages may be returned when the broker supplies another continuation
  token; applications continue until `next()` returns `None`.
- If `next()` fails, the token is unchanged and the application may retry.
- The SDK does not return continuation tokens to applications.
