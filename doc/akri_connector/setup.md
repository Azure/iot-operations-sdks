# Setup the Akri Operator

## Overview

The Akri Operator is currently in in private preview, and as such, is not deployed with Azure IoT Operations. Currently it is available to install via Helm using the following instructions:

## Prerequisites

1. A kubernetes cluster with [Azure IoT Operations deployed](/docs/setup.md).

## Setup

1. Deploy the Asset and Asset Endpoint Profile (AEP) custom resource definitions (CRD):

    ```bash
    helm install adr --version 0.2.0 oci://mcr.microsoft.com/azureiotoperations/helm/adr/assets-arc-extension -n azure-iot-operations
    ```

1. Install the Akri connector CRD and the Akri Operator:

    ```bash
    helm install akri-operator oci://akribuilds.azurecr.io/helm/microsoft-managed-akri-operator --version 0.4.0-main-20241016.1-buddy -n azure-iot-operations
    ```

1. Check the operator is deployed successfully:

    ```bash
    kubectl get pods -l app.kubernetes.io/instance=akri-operator
    ```

    output:

    ```output
    NAME                                            READY   STATUS    RESTARTS   AGE
    aio-akri-operator-deployment-6fbcdb9544-rltgz   1/1     Running   0          4m10s
    ```
