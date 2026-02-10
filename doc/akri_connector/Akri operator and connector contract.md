### Context
This document describes the contract between the Akri Operator and the connectors that it deploys using the `managedConfiguration` (image-based or StatefulSet-based) supplied via the `ConnectorTemplate`.

### Akri artifacts contract

#### Environment variables:
The following environment variables are set up for the connector by the operator.

- `CONNECTOR_ID`
    - The connector ID is the `pod` name set up for the connector. Each replica pod of a connector gets a distinct connector ID.
        - The connector ID is required to be used directly as the MQTT client ID. This is what is used for authorizing the mRPC requests to the Akri ADR service. Since the `pod` name remains consistent across pod restarts, this allows the MQTT client to leverage persistent MQTT sessions.
        - _The connector ID is currently required to be used directly as the MQTT client ID. If we need to support it being used as the MQTT client **prefix** then this support will need to be added in (we could use a pre-determined delimiter to separate the Akri-assigned prefix from the user-selected suffix)._
- `CONNECTOR_NAMESPACE`
    - The kubernetes namespace in which the Connector Pod is deployed or running.
- `CONNECTOR_CONFIGURATION_MOUNT_PATH`
    - The folder where the connector's configuration is available.
- `CONNECTOR_SECRETS_METADATA_MOUNT_PATH`
    - The folder where the metadata for connector secrets is available.
- `CONNECTOR_SECRETS_MOUNT_PATH`
    - The folder where connector secrets are available.
- `CONNECTOR_TRUST_SETTINGS_MOUNT_PATH`
    - The folder where the trust list certificates for the connector are available.
- `BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH`
    - The folder where the trust bundle for the broker is available.
- `BROKER_SAT_MOUNT_PATH`
    - The file containing the service account token for authentication to the broker.
- `ADR_RESOURCES_NAME_MOUNT_PATH`
    - The folder where the information about the device inbound endpoints and assets allocated to the connector is available.
- `DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH`
    - The folder where the trust bundle for the device inbound endpoints is available.
- `DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH`
    - The folder where the credentials for device inbound endpoints are available.

#### Stopgap Observability Environment variables:
- `OTLP_GRPC_METRIC_ENDPOINT`
    - Otel Grpc Metric Endpoint, will be passed as either secure (grpcs) or insecure (grpc) endpoint.
- `OTLP_GRPC_LOG_ENDPOINT`
    - Otel Grpc Log Endpoint, will be passed as either secure (grpcs) or insecure (grpc) endpoint.
- `OTLP_GRPC_TRACE_ENDPOINT`
    - Otel Grpc Trace Endpoint.
- `OTLP_HTTP_METRIC_ENDPOINT`
    - Otel HTTP Metric Endpoint.
- `OTLP_HTTP_LOG_ENDPOINT`
    - Otel HTTP Log Endpoint.
- `OTLP_HTTP_TRACE_ENDPOINT`
    - Otel HTTP Trace Endpoint.
- `FIRST_PARTY_OTLP_GRPC_METRICS_COLLECTOR_CA_PATH`
    - Mount location of the Trust Bundle on the local container host for First Party GRPC Metrics Collector.
- `FIRST_PARTY_OTLP_GRPC_LOG_COLLECTOR_CA_PATH`
    - Mount location of the Trust Bundle on the local container host for First Party GRPC Logs Collector.

**The connector application is required to read the configuration data from the mounted file system, and continue monitoring the file system for any updates to the configuration.**

#### Mounted file names and content schema:
- The `CONNECTOR_CONFIGURATION_MOUNT_PATH` env variable points to the `/etc/akri/config/connector_configuration` folder, which contains multiple files
    -  A file named`MQTT_CONNECTION_CONFIGURATION` with the following contents:
        ```json
        {
            "host": <value>,
            "keepAliveSeconds": <value>,
            "maxInflightMessages": <value>,
            "protocol": <value>,
            "sessionExpirySeconds": <value>,
            "tls": {
                "mode": <value>
            }
        }
        ```
        Note: All properties defined in this JSON will have values assigned.
    - Optionally, a file named `DIAGNOSTICS` with the following contents:
        ```json
        {
            "logs": {
                "level": <value>
            }
        }
        ```
        Note: If the value for log level is not specified in the `ConnectorTemplate` instance, this file will not be present.
    - Optionally, a file named `ADDITIONAL_CONNECTOR_CONFIGURATION` that contains stringifed json specified in `ConnectorTemplate.spec.runtimeConfiguration.managedConfigurationSettings.additionalConfiguration`. The schema of the contents in this file is defined by the connector image developer. This file will not be available if the `ConnectorTemplate.spec.runtimeConfiguration.managedConfigurationSettings.additionalConfiguration` is left empty on the connector template.

    - Optionally, a file named `PERSISTENT_VOLUME_MOUNT_PATH` that contains a list of PV mount paths, (separated by a newline). This is based on how the `ConnectorTemplate.spec.runtimeConfiguration.managedConfigurationSettings.persistentVolumeClaims[x].mountPath` and/or `ConnectorTemplate.spec.runtimeConfiguration.managedConfigurationSettings.persistentVolumeClaimTemplates[x].mountPath` are set up on the connector template. If there are no PVCs set up, this file will not be available.

- The `CONNECTOR_SECRETS_METADATA_MOUNT_PATH` env variable points to the `/etc/akri/config/connector_secrets_metadata` folder, which contains the mapping of each application-defined secret alias to the relative path where the secret is available on the file system. The connector is expected to:
    - Locate the file whose name matches the application-defined secret alias `{secret_alias}`.
    - Read the file’s content to obtain the relative path.
    - Concatenate the relative path to path from the env variable `CONNECTOR_SECRETS_MOUNT_PATH` to get the full path to the secret.

- The `CONNECTOR_SECRETS_MOUNT_PATH` env variable points to the `/etc/akri/secrets/connector_secrets` folder. This folder contains multiple subfolders that hold secrets required for the connector for authentication with external components.
    - The subfolders under `/etc/akri/secrets/connector_secrets` differ slightly based on whether unified `secretsync` is used or not.
        - If unified `secretsync` is used, all the secrets are available under a subfolder, assigned by Akri, with each secret in seperate files named using the `{secret_name}_{secret_key}` naming convention.
        - If unified `secretsync` is not used, each secret is avaialable under a `{secret_name}` subfolder, with each key in seperate files named after the `{secret_key}`.
    - Note: the connector developer doesn't need to worry the details of this structure. They should use the details from the alias metadata file in `/etc/akri/secrets/connector_secrets/secret_metadata/<secret_alias>` to get to the path to the secret as needed.

- The `CONNECTOR_TRUST_SETTINGS_MOUNT_PATH` env variable points to the `/etc/akri/secrets/connector_trust_settings` folder. This folder containes the certificates that all connector instances should trust.

- The `BROKER_TLS_TRUST_BUNDLE_CACERT_MOUNT_PATH` env variable points to the `/etc/akri/cacerts/broker_tls_trust_bundle` folder. This folder contains the trust bundle certificates that the connector can read to establish a TLS connection with the Broker.

- The `BROKER_SAT_MOUNT_PATH` env variable points to the `/etc/akri/secrets/broker-sat` file, which contains the service account token. This token is automatically refreshed, and the connector is expected to monitor this file to get the latest token.

- The `ADR_RESOURCES_NAME_MOUNT_PATH` env variable points to the `/etc/akri/config/adr_resources_names` folder. This folder contains multiple files, one for each device inbound endpoint allocated to the connector.
    - Filename: `{DeviceName}_{InboundEndpointName}`
    - Content: Asset names (separated by a newline)

- The `DEVICE_ENDPOINT_CREDENTIALS_MOUNT_PATH` env variable points to the `/etc/akri/secrets/device_endpoint_auth` folder. This folder contains multiple subfolders that hold secrets required for authentication with devices. For each device inbound endpoint, the connector is expect to append the relative path returned in the `getDevice` mRPC call to `/etc/akri/secrets/device_endpoint_auth` to get the full path to the secret.
    - The subfolders under `/etc/akri/secrets/device_endpoint_auth` differ slightly based on whether unified `secretsync` is used or not.
        - If unified `secretsync` is used, all the secrets are available under a subfolder, assigned by Akri, with each key in seperate files named using the `{DeviceName}_{InboundEndpointName}_{suffix}` naming convention, where suffix is one of `username`, `password`, `certificate`, `intermediatecertificates` or `certificatekey`
        - If unified `secretsync` is not used, each secret is avaialable under a `{secret_name}` subfolder, with each key in seperate files named after the `{secret_key}` referenced in the `Device` custom resource
    - Note: the connector developer doesn't need to worry the details of this structure. They can always use the details from the `getDevice` call to get to the secret/key as needed.

- The `DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH` env varaible points to `/etc/akri/cacerts/device_endpoint_tls_trust_bundle`.
    - this location contains multiple folders with one folder per unique secret among the inbound endpoints allocated to the particular Connector Instance.
    - This env variable is omitted if there are no Trust Lists associated with the inbound endpoints associated with this Connector Instance.
    - Folder names: `{TrustSettings.TrustList}`
    - Content of each folder: the trust bundle certificate that comes from mounting the secret set in the Device's InboundEndpoint's TrustSettings to the file location {DEVICE_ENDPOINT_TLS_TRUST_BUNDLE_CA_CERT_MOUNT_PATH}/{TrustSettings.TrustList}

    > The TrustList Secret name (which is used to determine the full path of the mounted secrets) is available to the connector application/runtime in the Device instance object retrieved via the Akri ADR service mRPC API.
