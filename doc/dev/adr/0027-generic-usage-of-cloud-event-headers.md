# ADR27: Generic usage of cloud event headers

## Context: 

> Note that this ADR builds upon previous ADRs [10](./0010-cloud-event-content-type.md) and [11](./0011-cloud-events-api.md).


There have been asks for our Azure Iot Operations Protocol package to support the creation of cloud event headers in a generic enough way that any application could take it as a dependency even if that application doesn't use our telemetry/RPC protocol clients.

Currently, our protocol package does allow users to pass in cloud event headers when sending telemetry messages, but the knowledge of what MQTT user property name is associated with each cloud event header is hidden within the protocol library.

## Decision: 

In each of our language protocol libraries, we will expose two new functions:

 1. One for parsing a "cloud event" type from a given set of MQTT user properties
 2. One for populating a set of MQTT user properties from a given "cloud event" type.

However, this new addition will __not__ replace the existing APIs for interacting with cloud event headers in the protocol's senders/receivers. See the [alternatives considered](#alternatives-considered) section for why those need to remain.

Additionally, each language protocol library should provide the same cloud event APIs for publishing RPC requests/responses as the telemetry sender/receiver currently have.

### Proposed API addition

```csharp
public class CloudEvent
{
    // The existing cloud event class and the fields it describes
    public Uri Source;
    public string SpecVersion;
    public string Type;
    public string? Id;
    public DateTime? Time;
    public string? DataContentType;
    public string? Subject;
    public string? DataSchema;

    // The two new APIs for converting to and from Mqtt user properties
    public List<MqttUserProperty> CreateMqttUserProperties();
    public static CreateFromMqttUserProperties(List<MqttUserProperty> mqttUserProperties);
}
```

## Alternatives Considered

1. Remove any awareness of cloud event headers from our protocol sender/receivers and only use a generic function for converting between cloud event header values and the corresponding MQTT user properties.
  - This would simplify our sender/receiver classes a bit since they no longer need to treat cloud headers any differently than other MQTT user properties, but...
  - The ```subject``` cloud event property should default to the MQTT topic that the MQTT message is published to, but this information is only available within the telemetry sender at publishing time. Taking this approach would 
    - Disallow the SDK from providing a default value for ```subject``` since we wouldn't know when a user intends a request to have cloud event headers
    - Make it difficult for the user to calculate the correct value for ```subject``` since the sender/receiver classes don't directly expose the target MQTT topic.