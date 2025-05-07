#!/bin/bash

set -e

# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import pollingrestthermostatconnector:latest -c k3s-default

# Build REST server docker image
docker build -t rest-server:latest ./SampleRestServer
docker tag rest-server:latest rest-server:latest
k3d image import rest-server:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-template.yaml

# Deploy REST server (as an asset)
kubectl apply -f ./KubernetesResources/rest-server.yaml

# Deploy REST server device and its two assets
kubectl apply -f ./KubernetesResources/rest-server-device-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset1-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset2-definition.yaml
