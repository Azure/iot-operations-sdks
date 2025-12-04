# ADR27: Generic usage of cloud event headers

## Context: 

> Note that this ADR builds upon previous ADRs [10](./0010-cloud-event-content-type.md) and [11](./0011-cloud-events-api.md).

There have been asks for our SDK to support the creation and parsing of cloud event headers in a generic enough way that any application could take it as a dependency even if that application doesn't use our telemetry/RPC protocol clients.

Currently, our protocol package does allow users to pass in cloud event headers when sending telemetry messages, but the knowledge of what MQTT user property name is associated with each cloud event header is hidden within the protocol library.

## Decision: 

In each of our language protocol libraries, we will expose two new functions on our MQTT message class:

 1. One that returns the "cloud event" struct (if applicable) in that mqtt message
 2. One for populating the relevant fields of that mqtt message when given a "cloud event" struct
   - This user flow should include the SDK providing default values of:
     - "1.0" for ```specversion```
     - The current UTC time for ```timestamp```
     - A new random Guid for ```id```.

However, this new addition will __not__ replace the existing APIs for interacting with cloud event headers in the protocol's senders/receivers. See the [alternatives considered](#alternatives-considered) section for why those need to remain.

Additionally, each language protocol library should provide the same APIs for publishing and receiving RPC requests/responses with cloud event headers as the telemetry sender/receiver currently have. The only difference will be that the default value for the ```type``` field will be either "ms.aio.rpc.request" or "ms.aio.rpc.response" depending on if the cloud event was emitted by a command invoker or a command executor.

### Proposed API addition

```csharp
public class CloudEvent
{
    // The existing cloud event class and the fields it describes
    public Uri Source;
    public string SpecVersion;
    public string Type;
    public string Id;
    public DateTime? Time;
    public string? DataContentType;
    public string? Subject;
    public string? DataSchema;
}

public class MqttApplicationMessage 
{
  //...

  public void SetCloudEvent(CloudEvent cloudEvent);

  public CloudEvent? GetCloudEvent()

  //...
}

```

### Example usage

#### Sending side

```csharp
public static void main()
{
    MqttClient client = new ...;
    client.ConnectAsync(...);

    CloudEvent cloudEvent = new CloudEvent()
    {
      Source = ...
      Type = ...
      DataSchema = ...
    };

    MqttApplicationMessage mqttMessage = new(...);

    // The appropriate headers are set and the content type is set
    mqttMessage.SetCloudEvent(cloudEvent);

    // Send the mqtt message with its cloud event
    client.PublishAsync(mqttMessage);

}
```

#### Receiving side

```csharp
public static void main()
{
    MqttClient client = new ...;
    client.ConnectAsync(...);
    sessionClient.ApplicationMessageReceivedAsync += (args) =>
    {
        Console.WriteLine("MQTT message received!");
        CloudEvent? cloudEvent = args.ApplicationMessage.GetCloudEvent();
        if (cloudEvent != null)
        {
          Console.WriteLine("It contained a cloud event with Id: " + cloudEvent.Id)
        }
    };

    client.SubscribeAsync(...);
}
```


## Alternatives Considered

1. Add two functions that: 
  - Take a list of MQTT user properties + MQTT message content type and return a cloud event struct
  - Take a cloud event struct and return a list of MQTT user properties + MQTT message content type
  - This approach is generic enough that a user could take these values and plug them into any MQTT client, but...
  - It requires a few extra steps in customer code compared to the proposed approach above
1. Remove any awareness of cloud event headers from our protocol sender/receivers and only use a generic function for converting between cloud event header values and the corresponding MQTT user properties.
  - This would simplify our sender/receiver classes a bit since they no longer need to treat cloud headers any differently than other MQTT user properties, but...
  - The ```subject``` cloud event property should default to the MQTT topic that the MQTT message is published to, but this information is only available within the telemetry sender at publishing time. Taking this approach would 
    - Disallow the SDK from providing a default value for ```subject``` since we wouldn't know when a user intends a request to have cloud event headers
    - Make it difficult for the user to calculate the correct value for ```subject``` since the sender/receiver classes don't directly expose the target MQTT topic.