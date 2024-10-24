# Service and SDK Limitations

## State Store

The state store does not support resuming sessions. In the case of a disconnect of the client, the client will need to reestablish the any observed keys. The has the following implications

1. Any key notifications that occurred when the client was disconnected are lost, the notifications should not be used for auditting or any other purposes that requires a guarantee of all notifications being delivered.

1. When reconnecting, the application is responsible for reading the state of any observed keys for changes that occurred during the disconnect. The client will notify the application that a reconnect has occurred.
