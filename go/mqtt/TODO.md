# TODO

This is the TODO list for MQTT session client development.

## Features
- [x] Implement an internal queue for messages to be published. Ensure messages
      are not lost if the connection to the MQTT broker is temporarily lost.
      Make the maximum size of this queue configurable. (required)
- [x] Implement a similar mechanism for managing subscribe/unsubscribe messages.
      This could utilize a goroutine or a queue. (required)
- [x] Set up a Last Will and Testament message (in Connect packet) for  broker
      if the client disconnects ungracefully. (required)
- [x] Add handlers for fatal connection or session failures to notify the user
      application. (required)
- [x] Amplify retryable error scenarios as specified. This could be achieved by
      excluding all non-retryable/fatal errors. (required)
- [ ] Abstract underlying Paho to hide primitive Paho types. 
      (optional, interface change)
- [x] Support packet logging to allow users to see sent/received packets.
      (required)
- [x] Support all connection settings within the connection string. (required)
- [ ] Enable multiple MessageHandler handlers to chain for message reception. 
      e.g., if different components using the same MQTT client 
      need to register different handlers for different tasks.
      (optional)
- [x] Support reauthentication based on MQTTv5 enhanced authentication. 
- [ ] Support checking the revocation status of the CA. 
- [ ] Allow users to refresh connection settings while the client is running. 
      e.g., User could implement a provider to refresh username/password/etc.,
      (optional)
- [ ] Add support for QoS 2. (future)
- [ ] Support resuming sessions/queues from disk or other backup storage. 
      (future)

## Testing
- [x] Add integration tests for connection/reconnection.
- [x] Add integration tests for subscribe/unsubscribe and subscribe option
      updates.
- [x] Add integration tests for publish functionality.
- [x] Add integration tests for message ordering and queueing.

## Improvements
- [ ] Return an `Ack()` function instead of directly acknowledging from the
      `MessageHandler`, allowing users to manually acknowledge messages at their
      convenience. (interface change)
- [ ] Address the lack of a mechanism in Paho for the caller to receive PUBACK
      on an async publish. 
      Since the session client cannot return the PUBACK,
      consider informing the user application when the publish packet is 
      dequeued and placed into the MQTT session. 
      This can be achieved by maintaining a
      parallel queue of callbacks alongside the publish queue. 
      But the most ideal way is to fix this in Paho. 
      They just haven't implemented it yet.
      Relevant Github issue: https://github.com/eclipse/paho.golang/issues/216

## Bugs
- [x] Fix the race condition in the `Subscribe` method. 
      e.g., calling `unsubscibe` and `updateOptions` at the same time 
      might cause unexpected subscription.

## Documentation
- [ ] Add explanations of behaviors, SDK usage, and samples in the spec.
      (could be in Golang comments) See https://go.dev/blog/examples
