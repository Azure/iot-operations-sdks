#!/bin/bash

set -o errexit
set -o pipefail

script_dir=$(dirname $(readlink -f $0))

# Install requirements
$script_dir/install-requirements.sh

# Prompt user if this is an interactive shell
if [[ $- =~ i ]]
then
    echo "This script will delete the default k3d cluster and create a new one."
    echo "Are you sure you want to proceed?"
    select yn in "Yes" "No"; do
        case $yn in
            Yes ) break;;
            No ) exit;;
        esac
    done
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
echo "mkdir ~/.kube; sudo install -o $USER -g $USER -m 600 /root/.kube/config ~/.kube/config"
echo =================================================================================================
