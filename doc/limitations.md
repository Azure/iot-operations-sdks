# Service and SDK Limitations

## State store

The state store does not support resuming sessions. In the case of a disconnect of the client, the client will need to reestablish the any observed keys. The has the following implications

1. Any key notifications that occurred when the client was disconnected are lost, the notifications should not be used for auditting or any other purposes that requires a guarantee of all notifications being delivered.

1. When reconnecting, the application is responsible for reading the state of any observed keys for changes that occurred during the disconnect. The client will notify the application that a reconnect has occurred.

## Clean Start

The SDK utilized `clean start` as false to persistent sessions during disconnects of the client. The SDK doesn't persist session data between application restarts. 

There are some edge cases where the application might be restarted after a PUBLISH is sent and an ACK is received. In these cases, the SDK has no way to resend the PUBLISH, as required by [4.1.0-1](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901231) of the MQTT spec.
