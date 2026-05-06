#!/bin/bash

set -e

# Build connector sample image. There is no southbound server to deploy: the
# sample uses an in-process FakeDevice (see Devices/FakeDevice.cs), which is the
# whole point of this sample.
dotnet publish /t:PublishContainer
k3d image import managementactionconnector:latest -c k3s-default

# Deploy connector template
kubectl apply -f ./KubernetesResources/connector-template.yaml

# Deploy device + asset (asset declares one Call, one Read, and one Write action
# under management group "device-control")
kubectl apply -f ./KubernetesResources/mgmt-action-device-definition.yaml
kubectl apply -f ./KubernetesResources/mgmt-action-asset-definition.yaml
