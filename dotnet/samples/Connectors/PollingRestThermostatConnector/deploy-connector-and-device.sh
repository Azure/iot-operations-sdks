#!/bin/bash

set -e

# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import pollingrestthermostatconnector:latest -c k3s-default

# Build REST server docker image
k3d image import mcr.microsoft.com/azureiotoperations/rest-test-server:0.4.0 -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-template.yaml

# Deploy REST server (as an asset)
# Export image name as environment variable and substitute in YAML
export SERVER_IMAGE_NAME
envsubst < ./KubernetesResources/rest-server.yaml | kubectl apply -f -

# Deploy REST server device and its two assets
kubectl apply -f ./KubernetesResources/rest-server-device-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset1-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset2-definition.yaml
