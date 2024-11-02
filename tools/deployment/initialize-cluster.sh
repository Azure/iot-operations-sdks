#!/bin/bash

set -o errexit
set -o pipefail

# install k3d
if [ ! $(which k3d) ]
then
    wget -q -O - https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash
fi

# install helm
if [ ! $(which helm) ]
then
    curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
fi


# install step
if [ ! $(which step) ]
then
    wget https://dl.smallstep.com/cli/docs-cli-install/latest/step-cli_amd64.deb -P /tmp
    dpkg -i /tmp/step-cli_amd64.deb
fi

# install az cli
if [ ! $(which az) ]
then
    curl -sL https://aka.ms/InstallAzureCLIDeb | bash
    az aks install-cli
fi

# install k9s
if [ ! $(which k9s) ]
then
    wget https://github.com/derailed/k9s/releases/latest/download/k9s_linux_amd64.deb -P /tmp
    dpkg -i /tmp/k9s_linux_amd64.deb
fi

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
echo "mkdir ~/.kube; sudo install -o $USER -g $USER /root/.kube/config ~/.kube/config"
echo =================================================================================================
