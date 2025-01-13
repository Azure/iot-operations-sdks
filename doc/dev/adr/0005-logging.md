# ADR5: Logging Alignments Between Languages

## Status: 

PROPOSED

## Context: 

While logging should not need to be 100% aligned between languages, it would be good to log the same events at the same logging level (to a reasonable extent) across languages.

## Decision:

All languages must have a way for customers to specify their minimum log level (including no logging).

All languages should offer flexibility in log destinations and formats if possible. For example, Rust utilizes a popular log crate within its SDK to generate logs. These logs can then be displayed to users through various public logger crates, each offering different levels of flexibility and functionality. We provide an example of how to use a common logger to display these logs in our samples, although customers are welcome to use alternative solutions.

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

Since different languages have very different implementations here, the only suggestions are as follows:
- The underlying MQTT Client's logs should be exposable, even though the log levels may not align exactly with our guidelines
- Any logs added in each language in the MQTT Package should follow the logging level guidelines defined above.

### Protocols
  > [!Note] Any logs that could be confused with MQTT logs (such as an info log that the Telemetry Receiver has initiated a Subscribe) should be clear that they are coming from the protocol package.

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
- ### State Store.
    - **Error**
      - Any errors that are returned to the application should get logged (errors creating the Client, sending requests, acking, etc)
      - Critical receive error (that ends use of the Client)
    - **Warn**
      - Errors on subscribe/unsubscribe/shutdown/receive that aren't returned
        - Error forwarding key notification to application because the application has closed the receiver.
      - Responses that don't indicate success but aren't errors?
      - Errors on parsing received key notifications that cause the message to be ignored, such as missing key name or version (application developer can't act on these, should not be logged as error)
      - Key Notification received that the Client isn't aware that it is observing that key (warn or debug?)
    - **Info**
      - Shutdown
      - Key Notification Receiver Started
      - Key Notification Receiver Stopped
    - **Debug**
      - Request sent (Set/Get/Del/Observe/Etc)
      - Response received
      - Key added to internally maintained list of observed keys (if relevant in language)
      - Key removed from internally maintained list of observed keys (if relevant in language)
      - Unexpected events around adding/removing a key from the internally maintained list of observed keys that don't affect the application. (if relevant in language)

## Consequences

-   Changes will need to be made across all languages to align with this decision.

## Open Questions

-   ~~Should errors returned to the application get logged, or should it be the responsibility of the application to log errors that it receives?~~ Resolved, we will log all of our errors so that they are always present in logs.
