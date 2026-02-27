# SMB Historian Connector Assessment

## Questions to Ask

- **What is the scenario?**
  - The connector polls SMB file shares for time-series CSV data and publishes telemetry to Azure IoT Operations via the MQTT broker, using the AIO StateStore for watermarks and optional leader election.
- **How often do you need time-based snapshots (hourly, daily, weekly)?**
  - Not specified. Scheduling is driven by `historian.cronExpression` in assets/datasets.
- **How do you handle schema changes?**
  - Not specified.
- **How do you monitor query success/failure?**
  - The connector provides structured logging, health checks, and success/failure metrics.
- **How do you manage credentials and rotation?**
  - Credentials can come from `appsettings.json`/env vars, Kubernetes Secrets, or Azure Key Vault in production. Rotation process is not specified.
- **Do you need audit logs for compliance?**
  - Not specified.
- **How large are these master data sets?**
  - Not specified.
- **Do you face latency or timeout issues during queries?**
  - There are configurable connection timeouts in `SMBConnector` settings.
- **What’s the business impact if a snapshot is delayed or incomplete?**
  - The Connector has logic to catchup, it is up to the user to configure.
- **How do you verify that the extracted data is complete and accurate?**
  - Not specified.

## Key Categories for Assessment

- **Setup Friction** (Drivers, TLS configuration, query setup, etc.)
  - Requires SMB access from the cluster, connector metadata publication, and asset/dataset configuration (base path, glob, schedule). TLS configuration details are not specified.
- **Security & Secrets Rotation** (Credential management and rotation for queries)
  - Supports env vars/Kubernetes Secrets and Azure Key Vault. Rotation specifics are not specified.
- **Observability** (Query status tracking, alerts, retries)
  - Health checks and structured logging/metrics are available. Alerting/retry policy is not specified.
- **Performance Bottlenecks** (Handling large datasets, network latency issues)
  - Not specified. There are settings for timeouts and max concurrent connections.
- **Change Resilience** (Schema evolution, endpoint changes)
  - Not specified.
