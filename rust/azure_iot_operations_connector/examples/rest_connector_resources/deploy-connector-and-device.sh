#!/bin/bash

set -e

# Know exact rest connector sample image
# Rest connector lives in another repository, so we need to know version from before.
# az acr login --name akribuilds.azurecr.io
# az acr update --name akribuilds.azurecr.io --anonymous-pull-enabled
# k3d image import akri-connectors/rest:0.2.0 -c k3s-default

# Build REST server docker image
cd ../../../../dotnet/samples/Connectors/PollingRestThermostatConnector
docker build -t rest-server:latest ./SampleRestServer
docker tag rest-server:latest rest-server:latest
k3d image import rest-server:latest -c k3s-default

# Deploy connector config
kubectl apply -f yamls/rest-connector-template.yaml

# Deploy REST server (as an asset)
kubectl apply -f ../../../../dotnet/samples/Connectors/PollingRestThermostatConnector/KubernetesResources/rest-server.yaml
# Deploy REST server device and its two assets
kubectl apply -f yamls/rest-sensor-device-def.yaml
kubectl apply -f yamls/rest-factory-device-def.yaml
kubectl apply -f yamls/rest-sensor-asset.yaml
kubectl apply -f yamls/rest-factory-asset.yaml
