# Service and SDK Limitations

## State store

The state store does not support resuming connections. In the case of a disconnect of the client, the client will need to re-observe keys of interest. This has the following implications:

1. Any key notifications that occurred when the client was disconnected are lost, the notifications should not be used for auditing or any other purpose that requires a guarantee of all notifications being delivered.

1. When reconnecting, the application is responsible for reading the state of any observed keys for changes that occurred while disconnected. The client will notify the application that a reconnect has occurred.

## Clean start

The session client utilized `clean_start` as false during a reconnect to automatically resume the last session. 

There are some edge cases where the application might be restarted after a PUBLISH is sent and an ACK is received. In these cases, the SDK has no way to resend the PUBLISH, as required by [4.1.0-1](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html#_Toc3901231) of the MQTT spec.
