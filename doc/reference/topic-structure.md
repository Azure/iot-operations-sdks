# Topic Structure

Telemetry and Command require topics to create the Envoys, these topics are defined in _topic template_ as a simple grammar composed with _tokens_.

## Requirements

Topic structure MUST be compatible with:

* [Unified Namespace (UNS)](https://youtu.be/wqcdqO6mCE8) requirements
* [OPC UA, PArt 14: PubSub](https://reference.opcfoundation.org/Core/Part14/v105/docs/) requirements

Topic structure SHOULD be compatible with:

* [SparkPlug specification](https://sparkplug.eclipse.org/specification/version/3.0/documents/sparkplug-specification-3.0.0.pdf) requirements.

## Topic Pattern

* A topic pattern is a sequence of _labels_ separated by `/`.
* Each _label_ is one of:
    * A string of printable ASCII characters not including space, `"`, `+`, `#`, `{`, `}`, or `/`
    * A *recognized token* from the first column in the tables below, specifically:
        * one of `{modelId}`, `{senderId}`, `{telemetryName}` for [Telemetry pattern](envoy-binder.md#telemetry-envoy)
        * one of `{modelId}`, `{executorId}`, `{invokerClientId}`, or `{commandName}` for [Command pattern](envoy-binder.md#command-envoy)
    * A *custom token*, defined as string that begins with `{ex:`, ends with `}`, and otherwise contains only ASCII alphabetic characters, with a minimum of one alphabetic character, e.g., `{ex:customToken}`
* The first label must not start with `$`

## Telemetry

Telemetry topic patterns MAY contain the following recognized tokens.

| Topic token | Description | Required |
| --- | --- | --- |
| `{modelId}` | The identifier of the the service model, which is the full DTMI of the Interface, might include the version | optional |
| `{senderId}` | Identifier of the asset that that sends Telemetry, by default the MQTT client ID of the asset | required |
| `{telemetryName}` | The name of the Telemetry | optional |

A *telemetry namespace* is an optional string of printable ASCII characters not including space, `+`, `#`, `{`, or `}`.
The string MAY contain one or more `/` characters, but it MUST NOT begin with a `/` character, MUST NOT end with a `/` character, and MUST NOT contain two or more `/` characters in succession.

A telemetry MQTT pub/sub topic is derived from a telemetry topic pattern in two steps.
In the first step, each topic token is replaced with a concrete string value.
For custom tokens, a replacement map is defined by the user; each map key is a non-empty string of ASCII alphabetic characters, and each map value follows the rules defined above for telemetry namespace.
For recognized tokens, the replacements are defined by the following table.

* For the `{telemetryName}` token, the replacement string is determined by the `name` property of the Telemetry.
* For the `{modelId}` token, the replacement string is determined by the `@id` property of the Interface (e.g., `"dtmi:example:TestVehicle;1"`)
* For the `{senderId}` token, the replacement string is determined by a property set by user code; if the property is not set, the local MQTT client is queried for its ID.

In the second step, if a telemetry namespace is defined by the user, the namespace is prepended to the token-substituted topic pattern, with an intervening `/` character.
If no telemetry namespace is defined by the user, the token-substituted topic pattern is used as the final topic for the telemetry.

### Telemetry topic example

An example of a permitted MQTT Telemetry topic pattern string is:

```text
sensors/{ex:dataPath}/{senderId}/telemetry
```

If the asset has identifier `sensor-temp-1628`, and if the user supplies a custom replacement map that contains the entry `"dataPath" : "west/building22"`, the resulting MQTT topic is:

```text
sensors/west/building22/sensor-temp-1628/telemetry
```

Furthermore, if the user has provided a telemetry namespace value of `deployments/gen/3`, the resulting MQTT topic becomes:

```text
deployments/gen/3/sensors/west/building22/sensor-temp-1628/telemetry
```

>[!NOTE]
> The string value of the `telemetryTopic` property is not required to contain the `{telemetryName}` token, but Telemetry communication differs depending on whether this token is present:
>
> * If the `telemetryTopic` property contains a `{telemetryName}` token, each Telemetry is assigned a separate MQTT pub/sub topic, and each is published separately. This may be more efficient for Telemetries that have no logical or temporal relationship to each other.
> * If the `telemetryTopic` property does not contain a `{telemetryName}` token, all Telemetries in the Interface are grouped into a collection that is published in a combined payload. This does not require any given payload to express values for all Telemetries, but it provides the option of including multiple Telemetries in a single published message.

## Command

Command topic patterns MAY contain the following recognized tokens.

| Topic token | Description | Required |
| --- | --- | --- |
| `{modelId}` | The identifier of the the service model, which is the full DTMI of the Interface | optional |
| `{executorId}` | Identifier of the asset that is targeted to execute a Command, by default the MQTT client ID of the asset | optional |
| `{invokerClientId}` | The MQTT client ID of the endpoint that invokes a Command | optional |
| `{commandName}` | The name of the Command | optional |

A *command namespace* is an optional string of printable ASCII characters not including space, `+`, `#`, `{`, or `}`.
The string MAY contain one or more `/` characters, but it MUST NOT begin with a `/` character, MUST NOT end with a `/` character, and MUST NOT contain two or more `/` characters in succession.

### Command request

A command request MQTT pub/sub topic is derived from a command topic pattern in two steps.
In the first step, each topic token is replaced with a concrete string value.
For custom tokens, a replacement map is defined by the user; each map key is a non-empty string of ASCII alphabetic characters, and each map value follows the rules defined above for command namespace.
For recognized tokens, the replacements are defined by the following table.

* For the `{commandName}` token, the replacement string is determined by the `name` of the Command. If the token is present, the command implementation MUST define a valid name according to the grammar.
* For the `{modelId}` token, the replacement string is determined by the `@id` property of the Interface (e.g., `"dtmi:example:TestVehicle;1"`)
* For the `{invokerClientId}` token, the replacement string is determined by querying the local MQTT client.
* For the `{executorId}` token, the replacement string is determined by a property set by user code; if the property is not set, the local MQTT client of the targeted asset is queried for its ID.

In the second step, if a command namespace is defined by the user, the namespace is prepended to the token-substituted topic pattern, with an intervening `/` character.
If no command namespace is defined by the user, the token-substituted topic pattern is used as the final topic for the command request.

### Command response

The response for a command MUST be published on the topic specified by the `ResponseTopic` MQTT property in the command request message.
There is no requirement for the structure or format of a response topic; this can be determined entirely by the user, as long as it accords with MQTT topic format rules.

If the user does not provide a function for generating response topics, they are derived by the following three steps.

The first step employs zero, one, or two values that are optionally supplied by the user: a *response prefix* and a *response suffix*, each of which follows the rules defined above for command topic patterns, including the allowability of topic tokens.

* If a response prefix is provided, it is prepended to the command topic pattern with an intervening `/` character.
* If a response suffix is provided, it is appended to the command topic pattern with an intervening `/` character.

In the second step, each topic token in the combined *prefix + pattern + suffix* string is replaced with a concrete string value.
For custom tokens, a replacement map is defined by the user; each map key is a non-empty string of ASCII alphabetic characters, and each map value follows the rules defined above for command namespace.
For recognized tokens, the replacements are defined by the table presented above with reference to deriving request topics.

In the third step, if a command namespace is defined by the user, the namespace is prepended to the token-substituted combined topic pattern, with an intervening `/` character.
If no command namespace is defined by the user, the token-substituted combined topic pattern is used as the final topic for the command response.

### Command topic example

An example of a permitted MQTT Command topic pattern string is:

```text
vehicles/{executorId}/command/{commandName}
```

If the executor Id is set by user code to `delivery-14`, and if the command name is `reset`, the resulting MQTT request topic is:

```text
vehicles/delivery-14/command/reset
```

Furthermore, if the user has provided a command namespace value of `region/quad12`, the resulting MQTT request topic becomes:

```text
region/quad12/vehicles/delivery-14/command/reset
```

If the user provides no response prefix or response suffix, the derived MQTT response topic is the same as the MQTT request topic above.
On the other hand, if the user has provided a response prefix of `clients/{invokerClientId}` and a response suffix of `response`, and if the invoker's client ID is `monitor5`, the resulting MQTT response topic is:

```text
region/quad12/clients/monitor5/vehicles/delivery-14/command/reset/response
```
