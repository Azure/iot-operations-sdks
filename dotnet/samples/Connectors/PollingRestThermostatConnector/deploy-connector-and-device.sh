#!/bin/bash

set -e

# Import images into k3d with verification + retry (guards against the silent
# `k3d image import` flake that surfaces later as ErrImageNeverPull).
source "$(dirname "$0")/../k3d-image-import.sh"

cd ./TestRestServer
./deploy-server.sh
cd ..

# Build connector sample image
dotnet publish /t:PublishContainer
k3d_image_import_with_retry pollingrestthermostatconnector:latest k3s-default

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-template.yaml

# Deploy REST server device and its two assets
kubectl apply -f ./KubernetesResources/rest-server-device-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset1-definition.yaml
kubectl apply -f ./KubernetesResources/rest-server-asset2-definition.yaml
