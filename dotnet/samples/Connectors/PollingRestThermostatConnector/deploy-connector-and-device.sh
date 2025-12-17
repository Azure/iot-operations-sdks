#!/bin/bash

set -e

# Configuration
SERVER_IMAGE_NAME="akribuilds.azurecr.io/rest-test-server:0.3.1"

if [ -z "$1" ]; then
    echo "Usage: $0 <server-image-name>"
    echo "Using default image name: $SERVER_IMAGE_NAME"
fi

# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import pollingrestthermostatconnector:latest -c k3s-default

# Build REST server docker image
docker build -t "$SERVER_IMAGE_NAME" ./SampleRestServer
docker tag "$SERVER_IMAGE_NAME" "$SERVER_IMAGE_NAME"
k3d image import "$SERVER_IMAGE_NAME" -c k3s-default

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
