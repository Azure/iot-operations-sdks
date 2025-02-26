#!/bin/bash

set -o errexit
set -o pipefail

script_dir=$(dirname $(readlink -f $0))

# Prompt user if this is an interactive shell
if [[ -t 0 ]]
then
    echo "This script will delete the default k3d cluster and create a new one."
    read -p "Are you sure you want to proceed? " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]
    then
        echo Cancelling $0
        exit
    fi
fi

echo =========================
echo Installing prerequisities
echo =========================

# install docker
SYSTEM_NAME=$(uname -r)
if ! [[ "$SYSTEM_NAME" == *"microsoft"* && "$SYSTEM_NAME" == *"WSL"* ]]
then
    if ! which docker;
    then
        sudo apt-get update
        sudo apt-get install -y docker.io
    fi
fi

# install k3d
if ! which k3d;
then
    wget -q -O - https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash
fi

# install helm
if ! which helm;
then
    curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
fi

# install step
if ! which step;
then
    wget -q https://github.com/smallstep/cli/releases/download/v0.28.0/step-cli_amd64.deb -P /tmp
    sudo dpkg -i /tmp/step-cli_amd64.deb
fi

# install az cli
if ! which az;
then
    curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
    az aks install-cli
fi

# install/upgrade extensions
az extension add --upgrade --name azure-iot-ops
az extension add --upgrade --name connectedk8s

# install k9s
if ! which k9s;
then
    wget -q https://github.com/derailed/k9s/releases/latest/download/k9s_linux_amd64.deb -P /tmp
    sudo dpkg -i /tmp/k9s_linux_amd64.deb
fi

echo =========================
echo Creating cluster
echo =========================

# Create k3d cluster and forwarded ports (MQTT/MQTTS)
k3d cluster delete
k3d cluster create \
    -p '1883:31883@loadbalancer' \
    -p '8883:38883@loadbalancer' \
    -p '8884:38884@loadbalancer' \
    --registry-create k3d-registry.localhost:127.0.0.1:5000 \
    --wait

# Set the default context / namespace to azure-iot-operations
kubectl config set-context k3d-k3s-default --namespace=azure-iot-operations

echo
echo =================================================================================================
echo The k3d cluster has been created and the default context has been set to azure-iot-operations.
echo If you need non-root access to the cluster, run the following command:
echo
echo "mkdir ~/.kube; sudo install -o $USER -g $USER -m 600 /root/.kube/config ~/.kube/config"
echo =================================================================================================
