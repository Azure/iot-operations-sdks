#!/bin/bash

set -o errexit
set -o pipefail

# install k3d
wget -q -O - https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash

# install Step
wget https://dl.smallstep.com/cli/docs-cli-install/latest/step-cli_amd64.deb
sudo dpkg -i step-cli_amd64.deb

# install Dapr
wget -q https://raw.githubusercontent.com/dapr/cli/master/install/install.sh -O - | /bin/bash

# Create k3d cluster and forwarded ports (MQTT/MQTTS)
k3d cluster delete
k3d cluster create \
    -p '1883:31883@loadbalancer' \
    -p '8883:38883@loadbalancer' \
    -p '8884:38884@loadbalancer' \
    --registry-create k3d-registry.localhost:127.0.0.1:5000 \
    --wait

# Set the default context / namespace
kubectl config set-context k3d-k3s-default --namespace=azure-iot-operations

# Install Dapr components
dapr init -k
