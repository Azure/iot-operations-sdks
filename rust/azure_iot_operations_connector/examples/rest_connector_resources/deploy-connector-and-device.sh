#!/bin/bash

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "Script running from directory: $SCRIPT_DIR"

# Know exact rest connector sample image
# Rest connector lives in another repository, so we need to know version from before.
# az acr login --name akribuilds.azurecr.io
# az acr update --name akribuilds.azurecr.io --anonymous-pull-enabled
# k3d image import akri-connectors/rest:0.2.0 -c k3s-default

# Build REST server docker image
cd ../../../../dotnet/samples/Connectors/PollingRestThermostatConnector
echo "Changed to directory: $(pwd)"
echo "Building REST server docker image..."
docker build -t rest-server:latest ./SampleRestServer
docker tag rest-server:latest rest-server:latest
k3d image import rest-server:latest -c k3s-default
echo "✓ Image imported to k3d cluster successfully"

echo "Changing back to script directory for YAML deployments..."
cd "$SCRIPT_DIR"
# Deploy connector config
kubectl apply -f ./yamls/rest-connector-template.yaml
echo "✓ Connector configuration deployed successfully"

# Deploy REST server (as an asset)
kubectl apply -f ../../../../dotnet/samples/Connectors/PollingRestThermostatConnector/KubernetesResources/rest-server.yaml
echo "✓ REST server deployed successfully"
# Deploy REST server device and its two assets
kubectl apply -f yamls/rest-sensor-device-def.yaml
kubectl apply -f yamls/rest-sensor-asset.yaml
echo "✓ REST sensor deployed"

kubectl apply -f yamls/rest-factory-device-def.yaml
kubectl apply -f yamls/rest-factory-asset.yaml
echo "Changed to directory: $(pwd)"
