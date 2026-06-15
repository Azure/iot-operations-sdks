#!/bin/bash

set -e

# Import images into k3d with verification + retry (guards against the silent
# `k3d image import` flake that surfaces later as ErrImageNeverPull).
source "$(dirname "$0")/../k3d-image-import.sh"

# Build connector sample image
dotnet publish /t:PublishContainer
k3d_image_import_with_retry sqlqualityanalyzerconnector:latest k3s-default

# Deploy SQL server (for the asset)
kubectl apply -f ./KubernetesResources/sql-server.yaml

# Deploy connector config
kubectl apply -f ./KubernetesResources/connector-template.yaml

# Deploy device and its lone asset
kubectl apply -f ./KubernetesResources/sql-server-device-definition.yaml
kubectl apply -f ./KubernetesResources/sql-server-asset-definition.yaml