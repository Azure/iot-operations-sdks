#!/bin/bash

set -e

# Import images into k3d with verification + retry (guards against the silent
# `k3d image import` flake that surfaces later as ErrImageNeverPull).
source "$(dirname "$0")/../k3d-image-import.sh"

# Build TCP thermostat client app
dotnet publish ../SampleTcpServiceApp /t:PublishContainer
k3d_image_import_with_retry sampletcpserviceapp:latest k3s-default 1

# Build connector sample image
dotnet publish /t:PublishContainer
k3d_image_import_with_retry eventdriventcpthermostatconnector:latest k3s-default 1

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-template.yaml

# Deploy TCP server (as an asset)
kubectl apply -f ./KubernetesResources/tcp-service.yaml

# Deploy TCP server device and its lone asset
kubectl apply -f ./KubernetesResources/tcp-service-device-definition.yaml
kubectl apply -f ./KubernetesResources/tcp-service-asset-definition.yaml
