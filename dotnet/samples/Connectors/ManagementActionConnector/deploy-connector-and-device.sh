#!/bin/bash

set -e

# Build connector sample image. There is no southbound server to deploy: the
# sample uses an in-process FakeDevice (see Devices/FakeDevice.cs), which is the
# whole point of this sample.
dotnet publish /t:PublishContainer
# Import images into k3d with verification + retry (guards against the silent
# `k3d image import` flake that surfaces later as ErrImageNeverPull).
source "$(dirname "$0")/../k3d-image-import.sh"
k3d_image_import_with_retry managementactionconnector:latest k3s-default

# Deploy connector template
kubectl apply -f ./KubernetesResources/connector-template.yaml

# Deploy device + asset (asset declares one Call, one Read, and one Write action
# under management group "device-control")
kubectl apply -f ./KubernetesResources/mgmt-action-device-definition.yaml
kubectl apply -f ./KubernetesResources/mgmt-action-asset-definition.yaml
