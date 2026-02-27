KubernetesResources for Akri.Connector.SMB

This folder contains minimal manifests to deploy the SMB connector and register its connector-metadata with the Akri operator / AIO environment.

Files:
- configmap-connector-metadata.yaml  -> exposes connectors/Akri.Connector.SMB/connector-metadata.json to the pod via a ConfigMap
- deployment.yaml                    -> Deployment for the connector (mounts the metadata and a PVC for watermark storage)
- serviceaccount.yaml                -> ServiceAccount used by the connector
- role.yaml                          -> Role granting lease permissions for leader election
- rolebinding.yaml                   -> RoleBinding for the ServiceAccount
- pvc.yaml                           -> PersistentVolumeClaim used to persist watermark DB locally
- secret-sample.yaml                 -> Placeholder for secrets (KeyVault, SMB password)

Usage:
1. Update values in deployment.yaml as needed (image name/tag, AIO broker/state endpoints, env vars).
2. Apply the manifests:
   kubectl apply -f connectors/Akri.Connector.SMB/KubernetesResources/

Notes:
- If you prefer to use the AIO StateStore for watermark persistence, remove the pvc volume and mount and ensure the connector is configured accordingly.
- Replace the sample secret with Kubernetes Secrets or an external provider in production.
