# MQTT Connection Settings

The MqttConnectionSettings class enables the operator to configure the MQTT Connection without recompiling the application.

## MqttConnectionSettings

### Types

> [!IMPORTANT]
> * [1] `TimeSpan` types are serialized using the [ISO8601 duration format](https://www.digi.com/resources/documentation/digidocs/90001488-13/reference/r_iso_8601_duration_format.htm).
> * **Null** default values are implemented different depending on the language.
### Settings

|Name|Environment variable|Required|Type|Default value|Description|
|-|-|-|-|-|-|
|`HostName`|`MQTT_HOST_NAME`|yes|string|n/a|FQDN to the endpoint, eg: mybroker.mydomain.com|
|`TcpPort`|`MQTT_TCP_PORT`|no|int|`8883`|TCP port to access the endpoint eg: 8883|
|`UseTls`|`MQTT_USE_TLS`|no|bool|`true`|Disable TLS negotiation (not recommended for production)|
|`CaFile`|`MQTT_CA_FILE`|no|string|null|Path to a PEM file to validate server identity|
|`CaRequireRevocationCheck`|`MQTT_CA_REQUIRE_REVOCATION_CHECK`|no|bool|`false`|Check the revocation status of the CA|
|`CleanStart`|`MQTT_CLEAN_START`|no|bool|true|Whether to use persistent session on first connect, subsequent connections will be `false`. Requires a unique `ClientId`.
|`KeepAlive`|`MQTT_KEEP_ALIVE`|no|TimeSpan[\[1\]](#types)|`P60S`|Interval of ping packets|
|`ClientId`|`MQTT_CLIENT_ID`|no|string|empty|MQTT Client Id, required for persistent sessions (`CleanStart=false`)|
|`SessionExpiry`|`MQTT_SESSION_EXPIRY`|no|TimeSpan[\[1\]](#types)|`P3600S`|Connection session duration|
|`ConnectionTimeout`|`MQTT_CONNECTION_TIMEOUT`|no|TimeSpan[\[1\]](#types)|`P30S`|Connection timeout|
|`Username`|`MQTT_USERNAME`|no|string|null|MQTT Username to authenticate the connection|
|`Password`|`MQTT_PASSWORD`|no|string|null|MQTT Password to authenticate the connection|
|`PasswordFile`|`MQTT_PASSWORD_FILE`|no|string|null|MQTT Password file|
|`CertFile`|`MQTT_CERT_FILE`|no|string|null|Path to a PEM file to establish X509 client authentication|
|`KeyFile`|`MQTT_KEY_FILE`|no|string|null|Path to a KEY file to establish X509 client authentication| 
|`KeyFilePassword`|`MQTT_KEY_FILE_PASSWORD`|no|string|null|Password (aka pass-phrase) to protect the key file| 
|`SatAuthFile`|`MQTT_SAT_AUTH_FILE`|no|string|null|Path to a file with the token to be used with SAT auth|

## Initialization

The MqttConnectionSettings class can be configured through the API, or with the use of environment variables or connection strings.

### Initialize from API

```cs
MqttConnectionSettings connSettings = new("aio-broker")
{
    TcpPort = 1883,
    UseTls = false
};
```

### Initialize from environment variables

```bash
export MQTT_HOST_NAME=aio-broker
export MQTT_TCP_PORT=1883
export MQTT_USE_TLS=false
```

```cs
MqttConnectionSettings connSettings = MqttConnectionSettings.CreateFromEnvVars();
```

### Initialize from connection string

```cs
string connectionString = "HostName=aio-broker;TcpPort=1883;UseTls=false";
MqttConnectionSettings connSettings = MqttConnectionSettings.CreateFromConnectionString(connectionString);
```

### Connect IMqttClient with MQTTConnectionSettings

```cs
IMqttClient mqttClient = MqttFactory().CreateMqttClient();
var connAck = await mqttClient.ConnectAsync(connSettings, stoppingToken);
```
