# "Counter" Sample

## Details
This sample application has two components

1) The [`counter_client`](./counter_client/), which sends "read" requests to the server regarding a counter value, and can also send "increment" requests to the server to increment the counter value

2) The [`counter_server`](./counter_server/), which responds to the requests from the client to send the value, as well as to increment it

The contents of the [`envoy`](./envoy) crate contains code generated from a DTDL defining the `counter` functionality to be used as a dependency. It can be regenerated using the `gen.sh` script.

## Prerequisites
This sample assumes the system has the required [environment variables](/doc/reference/connection-settings.md) set, and an MQTT broker running on the specified host that accepts the supplied credentials.
We recommend the use of the Azure IoT Operations MQ broker, but any broker will do for this sample.

## Running the sample
1) Navigate to the `counter_server` directory, and run the command `cargo run`.
2) Then, in a different terminal (or on a different machine), once the server is running, navigate to the `counter_client` directory and run the command `cargo run`
3) The sample will automatically exit when it is completed