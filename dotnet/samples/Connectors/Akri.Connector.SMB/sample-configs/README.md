# Sample Configurations

This directory contains sample configuration files for the SMB Historian Connector.

## Files

### smb-config.json

Complete sample configuration demonstrating:

1. **Parse Task** (`historical-data-parse`):
   - Polls SMB share hourly
   - Extracts CSV time-series data
   - Publishes to Azure IoT Operations MQTT broker
   - Tracks 3 data points: Temperature, Pressure, Humidity

2. **Copy Task** (`reference-docs-copy`):
   - Polls SMB share every 6 hours
   - Syncs PDF files to Kubernetes persistent volume
   - One-way sync with watermark-based incremental updates

## Usage

### Local Development

1. Copy sample configuration:
   ```bash
   cp sample-configs/smb-config.json appsettings.Development.json
   ```

2. Update connection details:
   ```json
   {
     "SMBConnector": {
       "Host": "your-smb-server.local",
       "ShareName": "YourShareName",
       "Username": "domain\\your-username",
       "Password": "your-password-here"
     }
   }
   ```

3. Run the connector:
   ```bash
   dotnet run
   ```

### Kubernetes Deployment

1. Create ConfigMap from sample:
   ```bash
   kubectl create configmap smb-connector-config \
     --from-file=appsettings.json=sample-configs/smb-config.json \
     -n default
   ```

2. Update Deployment to use ConfigMap:
   ```yaml
   containers:
   - name: connector
     volumeMounts:
     - name: config
       mountPath: /app/appsettings.json
       subPath: appsettings.json
   volumes:
   - name: config
     configMap:
       name: smb-connector-config
   ```

3. Override sensitive values with environment variables:
   ```yaml
   env:
   - name: SMBConnector__UseKeyVault
     value: "true"
   - name: SMBConnector__KeyVaultUrl
     value: "https://your-vault.vault.azure.net/"
   - name: SMBConnector__EnableLeaderElection
     value: "true"
   ```

## Validation

Validate configuration before deployment:

```bash
# Check JSON syntax
cat sample-configs/smb-config.json | jq .

# Test with dry-run (if supported)
dotnet run --validate-only

# Check required fields
cat sample-configs/smb-config.json | jq '.SMBConnector | keys[]'
```

## Configuration Options

### SMBConnector Section

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `Host` | Yes | SMB server hostname or IP | `smb-server.local` |
| `Port` | Yes | SMB port (default 445) | `445` |
| `ShareName` | Yes | SMB share name | `HistorianData` |
| `Username` | Yes | Authentication username | `domain\user` |
| `Password` | Conditional | Password (if not using Key Vault) | `secret123` |
| `UseKeyVault` | No | Enable Azure Key Vault for credentials | `true` |
| `BasePath` | Yes | Base directory path on share | `/data/historians` |
| `FilePattern` | Yes | Glob pattern for file filtering | `*.csv` |
| `TaskType` | No | Task type: `Parse` or `Copy` | `Parse` |
| `DestinationPath` | Conditional | Local path for Copy tasks | `/mnt/pv/docs` |
| `EnableLeaderElection` | No | Enable multi-pod HA | `true` |

### Queries Section

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `QueryId` | Yes | Unique query identifier | `historical-data` |
| `CronExpression` | Yes | Schedule (cron format) | `0 * * * *` |
| `WatermarkKind` | Yes | Always `Time` for SMB | `Time` |
| `WindowDuration` | Yes | Polling window size | `01:00:00` |
| `LookbackDuration` | Yes | Overlap for late data | `01:00:00` |
| `MaxWindowsPerTick` | No | Catch-up limit per cycle | `5` |
| `DataPoints` | Conditional | Required for Parse tasks | See sample |
| `TaskType` | No | Override connector-level task type | `Copy` |
| `FileFilter` | No | Override connector-level file pattern | `*.pdf` |
| `DestinationPath` | Conditional | Required for Copy tasks | `/mnt/pv/docs` |

## Security Notes

**Never commit credentials to version control!**

For production deployments:

1. Use Azure Key Vault for credentials:
   ```json
   {
     "UseKeyVault": true,
     "KeyVaultUrl": "https://your-vault.vault.azure.net/",
     "KeyVaultSecretName": "smb-password",
     "Password": ""
   }
   ```

2. Use Kubernetes Secrets for sensitive config:
   ```bash
   kubectl create secret generic smb-credentials \
     --from-literal=username='domain\user' \
     --from-literal=password='secret'
   ```

3. Reference secrets in Deployment:
   ```yaml
   env:
   - name: SMBConnector__Username
     valueFrom:
       secretKeyRef:
         name: smb-credentials
         key: username
   - name: SMBConnector__Password
     valueFrom:
       secretKeyRef:
         name: smb-credentials
         key: password
   ```

## See Also

- [README.md](../README.md) - Connector overview and features
- [quickstart.md](../../../specs/001-smb-connector/quickstart.md) - Deployment guide
- [spec.md](../../../specs/001-smb-connector/spec.md) - Requirements and user stories
