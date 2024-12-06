# ADR4: Logging Alignments Between Languages

## Status: 

PROPOSED

## Context: 

While logging should not need to be 100% aligned between languages, it would be good to log the same events at the same logging level (to a reasonable extent) across languages.

## Decision: 

As a general rule, this is what defines what log level a log should be categorized as:
- Error: Something the user should see and handle
  - Any errors thrown/returned to the application
- Warn: Something the user doesn't/can't handle
  - Log errors that aren't thrown/returned, but still aren't expected. Ex:
    - Received publish that contains a user property with reserved prefix
    - Executor receives an invalid command request and responds to the invoker (but the application isn't notified)  
- Info: Lifecycle information
  - Events that are good to know, but not constantly firing. A good rule of thumb is that these events should happen a known number of times per instance.
  - Examples: Invoker has been cleaned up, executor is subscribed.
- Debug: More detailed event logs, errors that are expected, and logs that could get spammy
  - Examples: logging on every publish sent/received, message received on a command invoker when there are no pending commands, etc.
- Trace: Not defined in this doc
  - Some recommendations: anything potentially sensitive - passwords, payloads, etc (allowed in debug as well if needed) or interesting state changes that aren't tied to user actions
  - **Note:** Go doesn't support trace level

## Specific Cases To Be Logged:
### MQTT
TODO

### Protocols
  - ### Telemetry
    - **Error**
      - Any errors that are returned to the application should get logged (errors creating the envoy, sending the message, acking, subscribing, etc)
      - Receiver
        - Critical receive error (that ends use of telemetry receiver)
    - **Warn**
      - Receiver
        - Errors on parsing received telemetry that cause the message to be ignored (application/receiver developer can't act on these, should not be logged as error)
        - Errors on parsing received telemetry that don't cause the message to be tossed, but indicate developer error on the other side of the pattern (ex. User property that starts with the reserved prefix that isn't one of ours). Value should be logged
        - Errors on subscribe/unsubscribe/shutdown
    - **Info**
      - Telemetry receiver
        - Subscribed
        - Unsubscribed
        - Shutdown
    - **Debug**
      - Sender
        - Telemetry sent
        - Telemetry acked? Maybe just this instead of sent and acked?
      - Receiver
        - Telemetry received
        - Telemetry acked? Only for manual? For both?
  - ### Command
    - **Error**
      - Any errors that are returned to the application should get logged (errors creating the envoy, sending the invoke/response, acking, subscribing, etc)
        - Invoker - errors parsing command response that get returned to the application
      - Critical receive error (that ends use of the Executor/invoker)
    - **Warn**
      - Errors on subscribe/unsubscribe/shutdown
      - Executor
        - Errors on parsing command requests that cause the request to be ignored (application/executor developer can't act on these, should not be logged as error). If this causes a response to be sent back to the invoker, information about what is sent should be included
        - Errors on parsing  command requests that don't cause the request to be tossed, but indicate developer error on the other side of the pattern (ex. User property that starts with the reserved prefix that isn't one of ours). Value should be logged
        - Execution timed out/got dropped by application and an error response is being automatically sent back to the invoker
        - Command expired so no response will be sent (debug or warn?)
      - Invoker
        - Errors on parsing  command responses that don't cause the response to be returned as an error, but indicate developer error on the other side of the pattern (ex. User property that starts with the reserved prefix that isn't one of ours). Value should be logged
    - **Info**
      - Subscribed
      - Unsubscribed
      - Shutdown
    - **Debug**
      - Invoker
        - Request sent
        - Request acked? Maybe just this instead of sent and acked?
        - Response received
        - Response acked?
        - Command responses that are not for this invoker
      - Executor
        - Request received
        - Request acked?
        - Response sent
        - Response acked?
  
### Services
TODO


## Consequences

-   Changes will need to be made across all languages to align with this decision.

## Open Questions

-   Should errors returned to the application get logged, or should it be the responsibility of the application to log errors that it receives?
