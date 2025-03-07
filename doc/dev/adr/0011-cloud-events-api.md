# ADR11: Cloud Event API Relationship

## Context: 

Cloud Events should be added for most/all MQ Telemetry Messages, and they must be optional. They must use the public format within user properties, which does not start with our reserved prefix. They are something we currently must easily support, but including cloud events on the Telemetry Message object opens the door for breaking API changes if we want to add easy functionality for other similar concepts in the future.

## Decision: 

The proposal is to have Cloud Events be built (in the Protocol Library) with convenience functions into `custom_user_data` and not a part of the `telemetry_message` API. This provides flexibility for us to add more similar things in the future without breaking changes, as well as the flexibility to change/deprioritize cloud events in the future if ever needed.

<details>
<summary>Psuedo Code Examples</summary>
<br>

### Current (Sending Side):
APIs
```rust
struct TelemetryMessage {
  cloud_event: Option<CloudEvent>,
  custom_user_data: Vec<(String, String)>,
  payload,
  qos,
  ...
}
pub fn new(/* language specific way of creating a Telemetry Message with all options */) -> TelemetryMessage;

struct CloudEvent {
  source,
  event_type,
  ...
}
pub fn new(/* language specific way of creating a Cloud Event with all options */) -> CloudEvent;
```
Use
```rust
// Create the cloud event struct
let cloud_event = CloudEvent::new(source: "aio://oven/sample", ...);
// Specify the cloud event on the telemetry message
let message = TelemetryMessage::new(cloud_event: cloud_event, payload: <payload>, ...);
```
### Proposed Example Implementation (Sending Side):
APIs
```rust
struct TelemetryMessage {
  custom_user_data: Vec<(String, String)>,
  payload,
  qos,
  // NO cloud_event field
  ...
}
pub fn new(/* language specific way of creating a Telemetry Message with all options */) -> TelemetryMessage;

struct CloudEvent {
  source,
  event_type,
  // NO datacontenttype field
  ...
}
pub fn new(/* language specific way of creating a Cloud Event with all options */) -> CloudEvent;

/// Takes in a cloud event object. Returns cloud event data as key/value pairs according to the Cloud Event MQTT spec. The returned value can be used as the `custom_user_data` field of the `TelemetryMessage`, appended to another array of key/value pairs and then used as the `custom_user_data` field of the `TelemetryMessage`, or have other key/value pairs appended to it and then used as the `custom_user_data` field of the `TelemetryMessage`
pub fn cloud_event_to_headers(cloud_event: CloudEvent) -> Vec<(String, String)> {
  // Converts `cloud_event` into key/value pairs with the correct data (for example, the `subject` value should be set as the telemetry topic, which this function has access to, and the `source` value should be set from the CloudEvent object)
}
```
Use
```rust
// create the cloud event struct
let cloud_event = CloudEvent::new(source: "aio://oven/sample", ...);
// Convert the cloud event into headers
let custom_user_data = telemetry_sender.cloud_event_to_headers(cloud_event);
// specify only custom_user_data (with cloud event data included) on the telemetry message
let message = TelemetryMessage::new(custom_user_data: custom_user_data, payload: <payload>, ...);
```

### Current (Receiving Side):
APIs
```rust
struct TelemetryMessage {
  cloud_event: Option<CloudEvent>,
  custom_user_data: Vec<(String, String)>,
  payload,
  qos,
  ...
}
struct CloudEvent {
  source,
  event_type,
  data_content_type,
  ...
}
```
Use
```rust
let telemetry_message = telemetry_receiver.recv().await;
let cloud_event = telemetry_message.cloud_event;
```
### Proposed Example Implementation (Receiving Side):
APIs
```rust
struct TelemetryMessage {
  custom_user_data: Vec<(String, String)>,
  payload,
  qos,
  // NO cloud_event field
  ...
}

struct CloudEvent {
  source,
  event_type,
  data_content_type,
  ...
}

/// Takes in the content_type of the message and `custom_user_data`, which is an array of key/value pairs that correlates to the MQTT user properties not defined by the SDK. Returns a complete CloudEvent object if present in the `custom_user_data` and there are no parsing errors. Ignores any irrelevant key/value pairs in `custom_user_data`
pub fn cloud_event_from_headers(content_type: String, custom_user_data: Vec<(String, String)>) -> Result<Option<CloudEvent>, Error>;
```
Use
```rust
let telemetry_message = telemetry_receiver.recv().await;
let cloud_event = cloud_event_from_headers(telemetry_message.content_type, telemetry_message.custom_user_data);
```
</details>

## Alternatives Considered:

1. No API change, but providing more documentation around when/how to use Cloud Events. This should still occur.

## Consequences:
API functions needed:
- Cloud Event Factory in language idiomatic way that provides default values for specified fields and validations on all fields as specified in [0010-cloud-event-content-type.md](./0010-cloud-event-content-type.md). Note that this will require knowledge of the Telemetry Sender for default values (such as the Telemetry Topic for the `subject` field). This Factory does _not_ take in or set the content_type MQTT Header. This should be managed through the serializer, as described in ADRs 10 and 12.
- to headers function - takes a CloudEvent object and returns an array formatted as User Properties  that can be added to `custom_user_data` that is passed in on the Telemetry Message.
- from headers function - Telemetry Receivers will return cloud event data raw as part of `custom_user_data` and the `content_type` field. This function will take in these User Properties and the MQTT content_type and returns a CloudEvent object.

Note: There is asymmetry between the CloudEvent object on the sending and receiving side for the `datacontenttype` field. This is intentional because per the Cloud Events MQTT spec, the `datacontenttype` is not included as a User Property, but is specified as the MQTT `content_type`. To reduce confusion and errors, the MQTT `content_type` will be settable through the Serializer, as specified in [0012-content-type.md](./0012-content-type.md). On the receiving side, however, the data from the MQTT `content_type` will be copied into the CloudEvent object for clarity.

Note: These API functions are likely present in all languages already, and just need to be made public/fully functioned
