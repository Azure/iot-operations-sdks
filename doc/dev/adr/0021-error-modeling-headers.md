# ADR 19: Modeling User Errors

## Context

We have received feedback from users that they would like to have RPC responses communicate application level failures in the headers of the response rather than the payload of the response. 

This feedback is, in part, because some applications must route RPC responses without reading/deserializing the entire payload. It is also partly because [some applications cannot extend or change the payload model to accommodate error handling.](https://github.com/Azure/iot-operations-sdks/issues/488#issuecomment-2707496996).

## Decision

Our SDKs will add APIs on the command executor side that mark an RPC response as an "application error" by attaching two new MQTT user properties ("AppErrCode" and "AppErrPayload") whose values are a user-defined string and a user-defined string (that is conventionally, but not necessarily, a stringified JSON object/value/array) respectively. When a user wants to indicate that an RPC call failed with an application error they must provide the "AppErrCode" value, but "AppErrPayload" is optional.

In addition, users must be able to include arbitrary user properties in their command response to support this error reporting in case our standard fields are insufficient. This feature is likely in place already for all languages.

On the command invoker side, we will add APIs for checking if a response was an application error and returning the error code and error data fields parsed from the MQTT message "AppErrCode" and "AppErrPayload" user properties.

Other than these two new user properties, the over-the-wire behavior of our protocol won't change as a result of this decision.

In order to provide a strongly-typed experience, we will also add codegen support for modeling the error codes in DTDL. This modeling will be detailed in a separate ADR.

## Code Example

### Command executor side

```csharp
public ExtendedResponse<IncrementResponsePayload> Increment(IncrementRequestPayload request)
{
    if (request.IncrementValue < 0)
    {
        var response = new ExtendedResponse<IncrementResponsePayload>()
        {
            Response = new IncrementResponsePayload { CounterResponse = _counter },
        };

        // Specify error code, but no error payload  
        response.WithApplicationError("negativeValue");

        // Or you can specify error code and error payload. The error payload can be any JSON primitive or a JSON object or a JSON array
        JsonNode? jsonPayload = JsonArray.Parse("[\"1\",\"2\"]");
        jsonPayload = JsonObject.Parse("{\"key\":\"value\"}");
        jsonPayload = JsonValue.Create(false);
        jsonPayload = JsonValue.Create(10);
        response.WithApplicationError("negativeValue", jsonPayload.toString());

        return response;
    }

    // happy path omitted
}
```

### Command invoker side

```csharp
public void main()
{
    MqttSessionClient mqttClient = ...;();
    CounterClient counterClient = new CounterClient(mqttClient);

    IncrementRequestPayload payload = new IncrementRequestPayload
    {
        IncrementValue = -1
    };

    var response = counterClient.Increment(executorId, payload).WithMetadata();
    
    // Check the RPC response for an application error
    if (response.TryGetApplicationError(out string? errorCode, out string? errorPayload))
    {
        // use the error code and strongly typed custom error payload type as wanted.
        JsonObject errorPayloadObject = JsonObject.Parse(errorPayload);
    }

    // Alternatively just fetch the error code if you don't want to deserialize the payload yet.
    if (response.TryGetApplicationError(out string? errorCode))
    {
        // ...
    }
}
```

### Enforcement

Note that, while this decision creates a standardized way of communicating application errors, our SDK will **not** enforce that users communicate application errors with this standard. Users will still be able to model application errors in response payloads or in custom user properties if they prefer. There is no way to force users to communicate errors with this pattern.

## Samples

We want to demonstrate this pattern in at least one sample per-language.

For the sake of demonstrating this in all languages similarly, we will make the counter service return an application error if the invocation specifies a negative value to increment by. This change won't include any DTDL level changes yet.

## Extra considerations

- This ADR does not change the decision made in (ADR 19)[./0019-codegen-user-errs.md] which allows users to model errors in payloads.
   - For applications where users could model errors in either headers or the payload, we should encourage them to model them in the payload.

## Open Questions

- MQTT user property value size limit considerations? Users may provide very large payload objects and MQTT as a protocol may not be built for that?