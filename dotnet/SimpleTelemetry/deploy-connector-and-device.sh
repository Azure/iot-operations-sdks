#!/bin/bash

set -e

# Build TCP thermostat client app
dotnet publish /t:PublishContainer
k3d image import simple-telemetry-tester:latest -c k3s-default

# Deploy connector config
kubectl apply -f ./telemetry-pod.yaml

