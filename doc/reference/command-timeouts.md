# Command Timeouts

This document describes the types of timeouts that are relevant to command execution, the relationship between these timeouts, and how the CommandExecutor should behave in response to various timeout conditions.

## Input values

There are two values that are available to the CommandExecutor when a request is received:

* `message expiry` - a value in the command metadata that arrives with the request
* `execution timeout` - a configuration value on the executor that applies to all requests

If a request does not include a `message expiry` value in its metadata, the request must be rejected as invalid.
This is necessary for proper caching.
For the cache to deduplicate requests correctly, it must store a computed response for the entire time in which a duplicate request can be received.
Responses can be trimmed from the cache only after the `message expiry` time has elapsed, which would never occur for a request that contains no `message expiry` value.

## Computed value

The following value is computed from the above two values:

* `cancellation timeout = min(message expiry, execution timeout)`

## CommandExecutor behavior

The CommandExecutor must determine the following three behavioral values:

1. how long to retain a result in the cache for deduplication
2. when to prompt the execution function to stop even if it has not finished
3. the message expiry value for the response message

These behavioral values are derived from the input and computed values as follows:

1. For request deduplication, the cache retention time equals the `message expiry`.
2. The `cancellation timeout` determines when the execution function is requested to stop even if it has not finished.
3. The expiry time for the response message is set to the remaining `message expiry` time at the moment the response message is sent.  If there is no remaining expiry time, no response is sent.

### Execution dispatching

Some CommandExecutor implementations will dispatch arriving requests to a thread pool.
For such executors:

* If a dispatched execution continues to execute beyond the `execution timeout`, it is cut short to restore the dispatching resource for use by subsequent requests.
* When a dispatch is cut short, a timeout response is sent to the invoker unless there is no remaining expiry time.
* After a dispatch is cut short, if the execution continues and eventually returns, the result value from the execution is ignored (and not sent to the CommandInvoker) even if some expiry time remains.

If a CommandExecutor implementation does not include a dispatcher, there is no need for a configured `execution timeout`.
The `cancellation timeout` will equal the `message expiry`, and the `execution timeout` value can be considered infinite for the analysis below.

## Analysis

The behavior varies depending on whether the `message expiry` or `execution timeout` is shorter.

### `message expiry` < `execution timeout`

* When the `message expiry` is less than the `execution timeout`, the CommandInvoker is effectively saying, "If I don't get the result soon, I'm moving on."
* The `cancellation timeout` will equal `message expiry`, so the execution function knows not to continue beyond when the result has a chance of being returned.
* If a result is returned by the execution function before the `cancellation timeout`, this must also be before the `message expiry`, so the result is sent in a response.
* If the `cancellation timeout` or `execution timeout` fires, no response is sent, because there is no remaining expiry time.  The broker would not forward the response, and the invoker is no longer listening.

### `message expiry` > `execution timeout`

* When the `message expiry` is greater than the `execution timeout`, the CommandInvoker is effectively saying, "I'm in no hurry (up to a point); take as long as you are willing."
* The `cancellation timeout` will equal `execution timeout`, so the execution function is politely requested to stop consuming resources beyond the `execution timeout`.
* If a result is returned by the execution function before the `cancellation timeout`, this must also be before the `message expiry`, so the result is sent in a response.
* If the `cancellation timeout` or `execution timeout` fires (these can race against each other), a timeout response is sent to notify the CommandInvoker that the execution was cut short.
