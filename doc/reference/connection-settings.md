# Connection Settings

The MqttConnectionSettings class enables the operator to configure the MQTT Connection without recompiling the application.

## MqttConnectionSettings

### Types

> [!IMPORTANT]
> * **Null** default values are implemented different depending on the language.
### Settings

|Name|Environment variable|Required|Type|Default value|Description|
|-|-|-|-|-|-|
|`Hostname`|`AIO_BROKER_HOSTNAME`|yes|string|n/a|FQDN to the endpoint, eg: mybroker.mydomain.com|
|`TcpPort`|`AIO_BROKER_TCP_PORT`|no|uint16|`8883`|TCP port to access the endpoint eg: 8883|
|`UseTls`|`AIO_MQTT_USE_TLS`|no|bool|`true`|Enable TLS negotiation (disabling not recommended for production)|
|`CaFile`|`AIO_TLS_CA_FILE`|no|string|null|Path to a PEM file to validate server identity|
|`CleanStart`|`AIO_MQTT_CLEAN_START`|no|bool|false|Whether to use persistent session on first connect, subsequent connections will be `false`. `true` requires a unique `ClientId`.
|`KeepAlive`|`AIO_MQTT_KEEP_ALIVE`|no|uint32|`60`|Interval of ping packets, in seconds|
|`ClientId`|`AIO_MQTT_CLIENT_ID`|no|string|empty|MQTT Client Id, required for persistent sessions (`CleanStart=false`)|
|`SessionExpiry`|`AIO_MQTT_SESSION_EXPIRY`|no|uint32|`3600`|Connection session duration, in seconds|
|`Username`|`AIO_MQTT_USERNAME`|no|string|null|MQTT Username to authenticate the connection|
|`PasswordFile`|`AIO_MQTT_PASSWORD_FILE`|no|string|null|MQTT Password file|
|`CertFile`|`AIO_TLS_CERT_FILE`|no|string|null|Path to a PEM file to establish X509 client authentication|
|`KeyFile`|`AIO_TLS_KEY_FILE`|no|string|null|Path to a KEY file to establish X509 client authentication| 
|`KeyPasswordFile`|`AIO_TLS_KEY_PASSWORD_FILE`|no|string|null|Password (aka pass-phrase) to protect the key| 
|`SatAuthFile`|`AIO_SAT_FILE`|no|string|null|Path to a file with the token to be used with SAT auth|

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
