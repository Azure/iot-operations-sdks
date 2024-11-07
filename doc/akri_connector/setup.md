# Setup the Akri Service

## Overview

The Akri Service is currently in **private preview**, and is not deployed with Azure IoT Operations. Currently it is available to install via Helm using the following instructions:

> [!WARNING] This GitHub Codespace for this repository comes preinstalled with the Akri service.

## Prerequisites

1. A Kubernetes cluster with [Azure IoT Operations deployed](/docs/setup.md).

## Setup

1. Deploy the ADR service, which contains the Asset and Asset Endpoint Profile CRDs:

    ```bash
    helm install adr --version 0.2.0 oci://mcr.microsoft.com/azureiotoperations/helm/adr/assets-arc-extension -n azure-iot-operations
    ```

1. Deploy the Akri Operator:

    ```bash
    helm install akri-operator oci://akribuilds.azurecr.io/helm/microsoft-managed-akri-operator --version 0.4.0-main-20241101.1-buddy -n azure-iot-operations
    ```
 
1. Install the Akri service, using `SAT` auth on `port 38884` for the MQTT broker:

    ```bash
    helm install akri oci://mcr.microsoft.com/azureiotoperations/helm/microsoft-managed-akri --version 0.5.8 -n azure-iot-operations \
    --set agent.extensionService.mqttBroker.useTls=true \
    --set agent.extensionService.mqttBroker.caCertConfigMapRef="azure-iot-operations-aio-ca-trust-bundle" \
    --set agent.extensionService.mqttBroker.authenticationMethod=serviceAccountToken \
    --set agent.extensionService.mqttBroker.hostName=aio-broker-external.azure-iot-operations.svc.cluster.local \
    --set agent.extensionService.mqttBroker.port=38884
    ```

