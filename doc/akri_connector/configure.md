# Akri Connector Configuration

The Akri connector is configured and deployted by the Akri Operator. The ADR client is used to read the configuration into the application.

Below are descriptions of the `AssetEndpointProfile`, `Asset` and `ConnectorConfig` CRDs.

## Asset endpoint profile

The asset endpoints are defined with the AssetEndpointProfile CRD.

```yaml
apiVersion: deviceregistry.microsoft.com/v1beta2
kind: AssetEndpointProfile
metadata:
  name: <endpoint_name>
  namespace: azure-iot-operations
spec:
  endpointProfileType: <endpoint_profile_type>
  targetAddress: <target_address>
  additionalConfiguration: <endpoint_configuration>
  authentication:
    method: Anonymous
  discoveredAssetEndpointProfileRef: <discovered_aep>
  uuid: <unique_uuid>
```

| Component | Description |
|-|-|
| `endpointProfileType` | Defines the configuration for the connector type that is being used with the endpoint profile. |
| `targetAddress` | The local valid URI specifying the network address/DNS name of a southbound device. The scheme part of the targetAddress URI specifies the type of the device.
| `additionalConfiguration` | Any additional configuration required by your Akri Connector |
| `authentication.method` | Defines the method to authenticate the user of the client at the server. `Anonymous`, `Certificate`, or `UsernamePassword`. |
| `discoveredAssetEndpointProfileRef` | Reference to a discovered asset endpoint profile. Populated only if the asset endpoint profile has been created from discovery flow. Discovered asset endpoint profile name must be provided. |
| `uuid` | Globally unique, immutable, non-reusable id. |

### Authentication

The following authentication methods are available:

1. Anonymous

    ```yaml
    spec:
      authentication:
        method: Anonymous
    ```

1. Certificate

    ```yaml
    spec:
      authentication:
        method: Certificate
        certificateSecretName: <certificate>
    ```

1. Username/password

    ```yaml
    spec:
      authentication:
        method: UsernamePassword
        usernamePasswordCredentials:
          usernameSecretName: <secret/username>
          passwordSecretName: <secret/password>
    ```

## Asset

Assets are assigned to an Asset Endpoint, and are individually addressable units.

```yaml
apiVersion: deviceregistry.microsoft.com/v1beta2
kind: Asset
metadata:
  name: <asset_name>
  namespace: azure-iot-operations
spec:
  displayName: <asset_display_name>
  description: <asset_description>
  assetEndpointProfileRef: <namespace>/<asset_endpoint_profile>
  defaultDatasetsConfiguration: |-
   {
      "samplingInterval": 4000,
   }
  defaultTopic:
    path: /mqtt/machine/status
    retain: Keep
  datasets:
    - name: thermostat_status
      dataPoints:
        - dataSource: /api/machine/my_thermostat_1/status
          name: actual_temperature
          dataPointConfiguration: |-
           {
              "HttpRequestMethod": "GET",
           }
        - dataSource: /api/machine/my_thermostat_1/status
          name: desired_temperature
          dataPointConfiguration: |-
           {
              "HttpRequestMethod": "GET",
           }
```

| Component | Description |
|-|-|
| `assetEndpointProfileRef` | The `AssetEndpointProfile` to assign the `Asset` to.
| `defaultDatasetsConfiguration` | Define the default configuration that applies to all datasets |
| `defaultTopic.path` | The default topic to use on the MQTT broker for the `Asset` |
| `defaultTopic.retain` | When retain should be set when publishing to the Mqtt broker |
| `datasets.name` | The name assigned to the data set |
| `datasets.datapoints.dataSource` | The source of the datapoint |
| `datasets.dataPoints.dataPointConfiguration` | Configuration of the datapoint |

## Connector Config

The `ConnectorConfig` CRD defines the configuration for the MQTT broker, the container image to be deployed and the endpoint profile type.

```yaml
apiVersion: akri.microsoft.com/v1
kind: ConnectorConfig
metadata:
  name: <connector_config_name>
  namespace: azure-iot-operations
spec:
  replicas: 1
  endpointProfileType: http-dss
  image: <connector_image>
  mqTargetAddress: aio-broker.azure-iot-operations.svc.cluster.local
  mqTls:
    enabled: true
    caTrustBundle:
      bundleName: azure-iot-operations-aio-ca-trust-bundle
      certificateName: ca.crt
  mqAuthentication:
    audience: aio-internal
```

| Component | Description |
|-|-|
| `endpointProfileType` | Defines the configuration for the connector type that is being used with the endpoint profile. |
| `image` | The container image to be deployed to the pod |
| `mqTargetAddress` | The address of the MQTT broker |
| `mqTls.enabled` | Is TLS enabled, `true` or `false` |
| `mqTls.caTrustBundle.bundleName` | The ConfigMap containing the trust bundle for the MQTT broker server |
| `mqTls.caTrustBundle.certificateName` | The name of the certificate in the trust bundle ConfigMap |
| `mqAuthentication.audience` | The audience used for SAT authentication |
