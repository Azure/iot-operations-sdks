#!/bin/bash

set -e

cd ./TestRestServer
./deploy-server.sh
cd ..

# Build connector sample image
dotnet publish /t:PublishContainer
k3d image import pollingrestthermostatconnector:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-template.yaml

# Deploy REST server device and its two assets
kubectl apply -f ./KubernetesResources/rest-server-device-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset1-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset2-definition.yaml
