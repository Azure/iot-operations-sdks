# SMB Historian Connector

The SMB Historian Connector enables Azure IoT Operations to retrieve time-series data from SMB (Server Message Block) file shares. This connector is designed for scenarios where historical data is stored in CSV files on network-attached storage or Windows file shares.

## Features

- **NTLM Authentication**: Secure authentication with Windows SMB servers
- **Azure Key Vault Integration**: Store credentials securely in Azure Key Vault
- **Glob Pattern Filtering**: Filter files using patterns like `*.csv` or `data_*.txt`
- **CSV Parsing**: Automatic parsing of time-series data from CSV files
- **File Copy Operations**: Sync files from SMB shares to Kubernetes persistent volumes
- **Watermarking**: Track processed files to avoid reprocessing
- **Leader Election**: Multi-pod HA deployments with Kubernetes leader election
- **Incremental Processing**: Only process files modified since last run
- **Health Checks**: Built-in health endpoints for SMB connectivity, leader status, and watermark store
- **Observability**: Structured logging and metrics for monitoring
- **Resilience Testing**: Comprehensive chaos and performance tests for production confidence

## Scenario overview

This repository contains an Akri connector that polls SMB file shares for time-series data (CSV) and publishes telemetry to the Azure IoT Operations (AIO) MQTT broker. The scenario demonstrates how to build, publish, and deploy a connector that:

- Connects to an SMB server/share using NTLM credentials (optionally retrieved from Azure Key Vault).
- Lists and reads files that match glob patterns (for example `*.csv`) under a configured base path.
- Parses CSV time-series files and publishes normalized telemetry messages to the AIO MQTT broker.
- Uses the AIO StateStore to persist per-query watermarks so processing is durable across restarts.
- Supports leader election in multi-pod deployments so only the active leader performs poll-and-publish work.

To implement and validate the scenario complete the following high-level tasks:

1. Build and publish the connector container image to the container registry configured in your AIO instance.

```bash
dotnet publish /t:PublishContainer -p:ContainerRegistry=<YOUR_REGISTRY>.azurecr.io -p:ContainerRepository=Akri.Connector.SMB -p:ContainerImageTag=latest
```

2. Author and publish `connector-metadata.json` (or provide it via a ConfigMap) so AIO can expose the connector type in the portal.

   Publishing connector metadata: ConfigMap vs ORAS

   - ConfigMap (development)
     - Use a Kubernetes `ConfigMap` to provide `connector-metadata.json` to the cluster for rapid iteration and testing. This repo includes `KubernetesResources/configmap-connector-metadata.yaml` as an example. Mount the ConfigMap into the connector pod or let the operator read it from the namespace.

   - ORAS (production)
     - Publish `connector-metadata.json` to your container registry using ORAS so AIO discovers the connector type in the portal. Example:

       ```bash
        # Linux
       oras push --config /dev/null:application/vnd.microsoft.akri-connector.v1+json \
         <YOUR_REGISTRY>.azurecr.io/connector-metadata:latest \
         connector-metadata.json:application/json
       ```

      ```bash
        # Windows
       oras push --config NUL:application/vnd.microsoft.akri-connector.v1+json \
         <YOUR_REGISTRY>.azurecr.io/connector-metadata:latest \
         connector-metadata.json:application/json
       ```

     - The `--config` media type `application/vnd.microsoft.akri-connector.v1+json` signals to AIO that this artifact is connector metadata. Use ORAS for CI/CD and production deployment.

   - Recommendation: use ConfigMap for local/dev testing and ORAS for staging/production. Ensure the `inboundEndpoints[].name` in the metadata (we use `smb-endpoint`) matches the `InboundEndpointName` used in Assets/Datasets.
3. Create a connector template instance in AIO (Connector templates → Create) using your published metadata.
    - In the Azure IoT Operations portal, create a connector template instance from your published connector metadata. This makes the connector available when creating device inbound endpoints.  
4. Provide or deploy an SMB source
   - Ensure the SMB share is accessible from your Kubernetes cluster. For testing you can deploy a sample SMB server reachable from the cluster or open network access to an existing share.
5. Create one or more Devices and Assets in the AIO operations experience. When adding an inbound endpoint to a Device, choose the endpoint type/name exposed by the connector metadata (the README examples use `smb-endpoint`).
6. Add Dataset(s) to the Asset that define queries (task type, source path, glob, schedule, and output topic). AIO persists assets; connectors watch and hot-reload dataset configurations.
   - In the AIO portal, create a Device and add an inbound endpoint that uses the connector template instance you created. Then create an Asset that references that inbound endpoint and add Dataset(s) that define your queries (task type, sourcePath, glob pattern, schedule, and output topic).
7. Verify the connector pod connects to the AIO MQTT broker and StateStore, registers queries, publishes telemetry, and persists watermarks.
   - Create a data flow in AIO that subscribes to the MQTT topic(s) the connector publishes to and routes messages to a sink such as Event Hubs. Verify messages arrive and that watermarks are advanced in the StateStore.

Minimal architecture (at a glance):

```
┌────────────────────────────┐    ┌─────────────────────┐
│ Kubernetes (connector pod) │───>│ AIO MQTT Broker     │
│ - SMB connector            │    │ (publish telemetry) │
│ - RQR engine + StateStore  │<──>│ AIO StateStore      │
└─────────────┬──────────────┘    └─────────────────────┘
              │
              ▼
      ┌──────────────────┐
      │ SMB Server/Share │
      └──────────────────┘
```

## Connector, Asset, and Dataset

- Connector: the running connector process (container/pod) that acts as the device-side worker. It connects to the AIO MQTT broker, watches the Asset & Device Registry (ADR), and executes queries.
- Asset: a logical description of a data source. In the SMB scenario the Asset contains connection details (for example `properties.smbConnection`) and default settings for datasets.
- Dataset: a per-query configuration inside an Asset (schedule, file glob, CSV format, output topic and dataset-level overrides). The connector maps Asset+Dataset → query definitions it executes.

## Configuration: how the connector gets SMB connection settings and queries

Connection topology (host, port, share) comes entirely from the **Device inbound endpoint address** in ADR — no static configuration is needed in the connector for these values. Credentials stay in `appsettings.json` / environment variables / Azure Key Vault because they are cluster-level secrets.

### Where each setting lives

| Setting | Source | How |
|---|---|---|
| SMB host, port, share | Device inbound endpoint `Address` field in ADR | Format: `smb://host/share` or `smb://host:port/share` |
| Base path on share | Asset or dataset attribute `basePath` | Dataset overrides asset |
| File glob pattern | Asset or dataset attribute `filePattern` | Dataset overrides asset |
| Username | `SMBConnector:Username` (env var or `appsettings.json`) | Cluster secret |
| Password | `SMBConnector:Password` or Key Vault secret | Cluster secret |
| Cron schedule | Asset or dataset attribute `historian.cronExpression` | Dataset overrides asset |
| Window duration | Asset or dataset attribute `historian.windowDurationSeconds` | Dataset overrides asset |

### Device inbound endpoint address

In the Azure IoT Operations portal, when you add an inbound endpoint to a Device using the SMB connector type, set the **Address** field to:

```
smb://smb-server.company.com/share-name
```

or with an explicit port:

```
smb://smb-server.company.com:445/share-name
```

This is parsed automatically by the connector — the host, port (default 445), and share name are extracted from the URL.

### Asset attributes

Set the following attributes on the Asset (or on individual Datasets to override per-query):

| Attribute key | Example | Description |
|---|---|---|
| `basePath` | `production` | Directory on the share to scan |
| `filePattern` | `*.csv` | Glob pattern for file matching |
| `historian.cronExpression` | `*/5 * * * *` | Cron schedule (default: every minute) |
| `historian.windowDurationSeconds` | `300` | Window duration in seconds (default: 60) |
| `historian.availabilityDelaySeconds` | `30` | Stability delay before window is queried (default: 0) |
| `historian.overlapSeconds` | `10` | Lookback overlap for late-arriving data (default: 0) |
| `historian.watermarkKind` | `Time` | `Time` or `Sequence` (default: `Time`) |

Dataset-level values (set in the **Dataset configuration** JSON field in the portal) override asset-level attributes for the same keys.

### Authentication options

- **`appsettings.json` / env vars** (development): set `SMBConnector:Username` and `SMBConnector:Password`. See the local development section below.
- **Kubernetes Secret** (staging): mount a `Secret` and expose fields as `SMBConnector__Username` / `SMBConnector__Password` environment variables in the pod.
- **Azure Key Vault** (production): set `SMBConnector:UseKeyVault=true`, `SMBConnector:KeyVaultUrl`, and `SMBConnector:KeyVaultSecretName`. The connector fetches the password at runtime.

### Local development (`appsettings.json`)

Only credential and tuning settings belong here. Connection topology and query parameters come from ADR at runtime.

```json
{
  "SMBConnector": {
    "Username": "historian-reader",
    "UseKeyVault": false,
    "Password": "your-password-here",
    "ConnectionTimeoutSeconds": 30,
    "MaxConcurrentConnections": 10,
    "MaxFileSizeBytes": 10485760,
    "EnableLeaderElection": false
  }
}
```

### Production (Azure Key Vault)

```json
{
  "SMBConnector": {
    "Username": "historian-reader",
    "UseKeyVault": true,
    "KeyVaultUrl": "https://your-keyvault.vault.azure.net/",
    "KeyVaultSecretName": "smb-password",
    "EnableLeaderElection": true,
    "LeaderElectionNamespace": "default",
    "LeaderElectionLeaseName": "smb-connector-leader"
  }
}
```

Using the `KubernetesResources` manifests
- Use `connectors/Akri.Connector.SMB/KubernetesResources/` for local and cluster deployments. Important manifests:
  - `configmap-connector-metadata.yaml`: exposes `connector-metadata.json` to the cluster for development.
  - `deployment.yaml`: example Deployment. Update image, credential env vars, and PVC name before applying.
  - `secret-sample.yaml`: example showing how to provide SMB credentials or Key Vault settings as Kubernetes Secrets.
  - `pvc.yaml`: used to persist watermark storage.
  - `serviceaccount.yaml`, `role.yaml`, `rolebinding.yaml`: RBAC required for leader election.


## Author connector metadata configuration

The `connector-metadata.json` file in this directory is the authoritative source. Key fields to update for your environment:

```json
{
    "name": "Mesh.Akri.SMBConnector",
    "version": "1.0.2",
    "imageConfigurationSettings": {
        "imageName": "<YOUR_REGISTRY>.azurecr.io/akri-connector-smb",
        "tag": "latest"
    },
    "inboundEndpoints": [
        {
            "endpointType": "MeshSystems.SMB",
            "fields": {
                "address": {
                    "exampleValue": "smb://smb-server.local/HistorianData",
                    "description": "SMB URL: smb://host/share or smb://host:port/share"
                }
            }
        }
    ]
}
```

The full schema including dataset configuration is in `connector-metadata.json`.

## Create a connector template instance

Follow these steps in the Azure IoT Operations portal to create a connector template instance so operators can add inbound endpoints that use this connector type:

1. Sign in to the Azure portal and open your Azure IoT Operations instance.
2. Go to **Components > Connector templates** and select **+ Create a connector template**.
3. Choose the connector type that corresponds to this connector (it appears after you publish the metadata via ORAS or provide it as a ConfigMap).
4. On the **Metadata** page, enter a name for the connector template instance. This name is used as the pod name prefix when the operator creates connector pods.
5. Continue through **Device inbound endpoint type**, **Diagnostics configuration**, **Runtime configuration**, and **Review** pages. Make sure the endpoint type/name you select matches the `inboundEndpoints[].name` in `connector-metadata.json` (for example `smb-endpoint`).
6. Select **Create**. The connector template instance is now available when creating Devices and Assets in the operations experience.

Note: use a ConfigMap for rapid development iterations and ORAS-published metadata for staging/production so the portal discovers your connector reliably.

### If the connector does not show in the portal

If the ACR is in a different tenant from the AIO instance you can create or update the connector template instance directly using the Azure IoT Operations REST API. See the official docs for the Create or Update connector template API:

https://learn.microsoft.com/en-us/rest/api/iotoperations/akri-connector-template/create-or-update?view=rest-iotoperations-2025-10-01&tabs=HTTP

Example using `az rest` (replace placeholders):

```powershell
# Prepare body.json with the connector template definition that references your ORAS-published metadata
az account get-access-token --resource https://management.azure.com/
az rest --method put \
  --uri "https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.IoTOperations/akriConnectorTemplates/{connectorTemplateName}?api-version=2025-10-01" \
  --body @body.json
```

Create a minimal `body.json` that points to your metadata artifact (the exact schema is documented on the API page). Using the REST API ensures the connector template is registered immediately in the AIO control plane when the portal UI does not surface it.

## Azure IoT Operations Asset Configuration

Queries are configured as **Datasets** on an AIO Asset. Each Dataset maps to one scheduled query.

### Asset attributes (connection topology and defaults)

Set these on the Asset itself to apply to all its datasets:

```
basePath            = production
filePattern         = *.csv
cronExpression = */1 * * * *
```

### Dataset configuration (per-query overrides)

In the **Dataset configuration** JSON field in the portal you can override any asset-level attribute for that specific dataset:

```json
{
  "basePath": "/sensors/temperature/",
  "filePattern": "temp_*.csv",
  "cronExpression": "*/5 * * * *",
  "windowDurationSeconds": 300
}
```

### Query Types

#### Parse Query (default)

Parses CSV files and publishes time-series data as MQTT telemetry. Set `smb.basePath` and `smb.filePattern` on the asset/dataset. No extra `taskType` setting is needed — Parse is the default.

#### Copy Query

Copies files from SMB to a local Kubernetes Persistent Volume. Set `SMBConnector:TaskType=Copy` and `SMBConnector:DestinationPath=/mnt/pv/destination` in the connector configuration (these are pod-level settings, not per-query).

### Multiple Datasets per Asset

Each dataset gets its own watermark and schedule. Example: two datasets on the same asset targeting different directories on the same share:

| Dataset | `basePath` | `filePattern` | `cronExpression` |
|---|---|---|---|
| `temperature-data` | `/sensors/temperature/` | `temp_*.csv` | `*/1 * * * *` |
| `pressure-data` | `/sensors/pressure/` | `press_*.csv` | `*/5 * * * *` |

Per-Query Schedule Resolution

| Attribute key | Description | Default |
|---|---|---|
| `cronExpression` | Cron schedule | `* * * * *` |
| `windowDurationSeconds` | Window duration | `60` |
| `availabilityDelaySeconds` | Stability delay | `0` |
| `overlapSeconds` | Lookback overlap | `0` |
| `watermarkKind` | `Time` or `Sequence` | `Time` |

### Watermark Management

Each dataset maintains its own watermark (last processed file timestamp) independently.

## CSV File Format

The connector expects CSV files with the following format:

```csv
timestamp,tag,value,quality
2026-02-04T10:00:00Z,Temperature.Sensor1,23.5,0
2026-02-04T10:01:00Z,Temperature.Sensor1,23.6,0
2026-02-04T10:00:00Z,Pressure.Sensor1,101.3,0
```

### Columns

1. **timestamp**: ISO 8601 formatted timestamp (UTC recommended)
2. **tag**: Tag name / data point identifier
3. **value**: Numeric value
4. **quality** (optional): Quality indicator (0 = Good, non-zero = Bad)

## Deployment

### Docker Build

```bash
docker build -f connectors/Akri.Connector.SMB/Dockerfile -t smb-connector:latest .
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: smb-connector
spec:
  replicas: 3
  selector:
    matchLabels:
      app: smb-connector
  template:
    metadata:
      labels:
        app: smb-connector
    spec:
      serviceAccountName: smb-connector
      containers:
      - name: connector
        image: smb-connector:latest
        env:
        - name: SMBConnector__Username
          value: "historian-reader"
        - name: SMBConnector__UseKeyVault
          value: "true"
        - name: SMBConnector__KeyVaultUrl
          value: "https://your-keyvault.vault.azure.net/"
        - name: SMBConnector__KeyVaultSecretName
          value: "smb-password"
        - name: SMBConnector__EnableLeaderElection
          value: "true"
      volumes:
      - name: watermark-storage
        persistentVolumeClaim:
          claimName: smb-connector-watermark
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: smb-connector
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: smb-connector-leader-election
rules:
- apiGroups: ["coordination.k8s.io"]
  resources: ["leases"]
  verbs: ["get", "create", "update"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: smb-connector-leader-election
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: smb-connector-leader-election
subjects:
- kind: ServiceAccount
  name: smb-connector

## Diagnose the connector (accessing customer AIO tenant)

Prerequisite
- You have access to the customer's Azure tenant and the necessary RBAC permissions to run `az connectedk8s proxy` and to query the cluster.
- The AIO cluster must be reachable from your workstation. (TODO: investigate recommended secure proxy patterns for exposing AIO cluster for diagnostics.)

High-level steps
1. Login to Azure using the customer tenant:

```sh
az login --tenant <tenant-uuid-here>
```

2. In a separate terminal (requires admin privileges), run the connected k8s proxy command to expose a local proxy to the connected cluster:

```sh
az connectedk8s proxy --name <aio-cluster-here> --resource-group <rg-here>
```

- TODO: If the customer uses a different connectivity pattern (VPN, bastion, or direct cluster API), adapt the proxy step accordingly.

3. In your working terminal (no admin required) use `kubectl` against the proxied cluster:

```sh
kubectl get pods -n azure-iot-operations
```

4. Identify the connector pod(s) and retrieve diagnostics:

- List pods with details:

```sh
kubectl get pods -n azure-iot-operations -o wide
```

- Describe the pod to see events and status:

```sh
kubectl describe pod <pod-name> -n azure-iot-operations
```

- Stream logs to observe startup, asset discovery and runtime behavior:

```sh
kubectl logs -f <pod-name> -n azure-iot-operations
```

- If the pod has multiple containers (sidecars), target the connector container:

```sh
kubectl logs -f <pod-name> -c connector -n azure-iot-operations
```

5. Search logs for key diagnostic messages:
- `Starting to observe devices`
- `Asset available`
- `Registered historian query`
- `RQR engine initialized`
- `Published batch`
- `State store not available` or `NoMatchingSubscribers` (watermark persistence issues)

6. Verify MQTT connectivity and StateStore interactions:
- Check connector logs for MQTT connect/subscribe/publish messages.
- If you can locally subscribe to MQTT, use the broker endpoint or port-forward to observe telemetry topics.

7. Live troubleshooting inside the pod (if needed):

```sh
kubectl exec -it <pod-name> -n azure-iot-operations -- /bin/bash
```

8. Collect artifacts for escalation:
- `kubectl describe pod <pod-name> -n azure-iot-operations`
- Pod logs (`kubectl logs`) — last 1k-10k lines
- Connector configuration (ConfigMap or mounted files)
- `sample-config.json` used for local debugging
- Timestamps for problematic runs

Notes
- Coordinate with the customer for secure access and to avoid exposing production systems unnecessarily.
- If you lack permissions to run the proxy, ask the customer admin to run it and provide a captured log or temporary access.

```

## Performance

The connector is designed to meet the following performance criteria:

- **SC-001**: SMB connection establishment in under 10 seconds
- **SC-002**: File listing for up to 1,000 files in under 5 seconds
- **SC-003**: Data retrieval from 10MB files in under 30 seconds

### Scale Limits

- Maximum 1,000 files per directory
- Maximum 10MB per file (larger files are skipped with a warning)
- Maximum 10 concurrent connections

## Troubleshooting

### Connection Issues

If you see "Failed to connect to SMB server" errors:

1. Verify the SMB server is reachable: `ping smb-server.local`
2. Check port 445 is accessible: `telnet smb-server.local 445`
3. Verify credentials are correct
4. Check SMB server logs for authentication failures

### File Access Issues

If you see "Failed to open directory" errors:

1. Verify the share name is correct
2. Check the base path exists on the share
3. Verify the user has read permissions on the share and directory

### Watermark Issues

If files are being reprocessed incorrectly:

1. Check the watermark database path is on persistent storage
2. Verify the database file has read/write permissions
3. Check logs for watermark update errors

### Leader Election Issues

If multiple pods are processing files in multi-pod deployments:

1. Verify `EnableLeaderElection` is set to `true`
2. Check RBAC permissions for lease resources
3. Review leader election logs for errors

## Health Checks

The connector provides health check endpoints for monitoring and Kubernetes probes:

### Available Health Checks

1. **SMB Connectivity** (`/health/smb`): Verifies SMB share is accessible
2. **Leader Election** (`/health/leader`): Reports leader election status
3. **Watermark Store** (`/health/watermark`): Validates watermark store configuration

### Kubernetes Health Probes

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 30
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
```

Health check responses include detailed status information:

```json
{
  "status": "Healthy",
  "checks": {
    "smb_connectivity": {
      "status": "Healthy",
      "description": "SMB share is accessible and base path exists"
    },
    "leader_election": {
      "status": "Healthy",
      "data": {
        "IsLeader": true,
        "LeaseHolderIdentity": "smb-connector-abc123"
      }
    }
  }
}
```

## Task Types

The connector supports two task types for different use cases:

### Parse Task

Extracts time-series data from CSV files and publishes to MQTT:

```json
{
  "QueryId": "historical-data",
  "TaskType": "Parse",
  "DirectoryPath": "/data",
  "FileFilter": "*.csv",
  "Schedule": "0 * * * *"
}
```

### Copy Task

Syncs files from SMB share to Kubernetes persistent volume:

```json
{
  "QueryId": "reference-docs",
  "TaskType": "Copy",
  "DirectoryPath": "/docs",
  "FileFilter": "*.pdf",
  "DestinationPath": "/mnt/pv/reference-docs",
  "Schedule": "0 * * * *"
}
```

Copy task features:

- Atomic file operations (temp file + atomic rename)
- Idempotent (watermark prevents duplicates)
- One-way sync (source deletions logged but not mirrored)
- Failed copies don't advance watermark (automatic retry)

## Testing

The connector includes comprehensive test suites:

### Test Categories

1. **Unit Tests**: Connection, file listing, CSV parsing
2. **Integration Tests**: Copy operations, watermark tracking
3. **Data Retrieval Tests**: CSV parsing accuracy, tag mapping, quality handling
4. **Chaos Tests**: Network failures, disk full, pod restarts, partial writes
5. **Performance Tests**: File listing speed, large file processing, concurrent operations

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "FullyQualifiedName~DataRetrievalTests"
dotnet test --filter "FullyQualifiedName~ChaosTests"
dotnet test --filter "FullyQualifiedName~PerformanceTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Architecture

```
┌─────────────────────────────────────────────────┐
│         SMB Historian Connector                 │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌──────────────┐         ┌─────────────────┐   │
│  │   Program    │--------▶│HistorianWorker  │   │
│  └──────────────┘         └─────────────────┘   │
│                                   │             │
│                                   ▼             │
│                          ┌─────────────────┐    │
│                          │  SMBHistorian   │    │
│                          │    Executor     │    │
│                          └─────────────────┘    │
│                                   │             │
│              ┌────────────────────┼─────────┐   │
│              ▼                    ▼          ▼  │
│    ┌────────────────┐  ┌──────────────┐ ┌────┴────┐
│    │   SMBClient    │  │ Watermark    │ │ Leader  │
│    │                │  │   Store      │ │Election │
│    └────────────────┘  └──────────────┘ └─────────┘
│              │                 │              │ │
│              │        ┌────────▼─────┐        │ │
│              │        │ Health       │        │ │
│              │        │ Checks       │        │ │
│              │        └──────────────┘        │ │
└──────────────┼────────────────────────────────┼─┘
               ▼                                ▼
    ┌──────────────────┐              ┌─────────────┐
    │   SMB Server     │              │ Kubernetes  │
    │   (File Share)   │              │ PV Storage  │
    └──────────────────┘              └─────────────┘
```

## License

Copyright © 2026 Mesh Systems LLC. All rights reserved.
