#!/bin/bash

set -o errexit
set -o pipefail

help()
{
    echo "Usage: initialize-cluster.sh [OPTION]..."
    echo "Initialize cluster will install prerequisites and create a k3s cluster."
    echo
    echo "Options:"
    echo "-h    Print this Help."
    echo "-s    Skip the installation of prerequisites."
    echo "-y    Automatically assume 'yes' to all questions."
    echo
}

apt-get-update-with-retry()
{
    local max_attempts=3
    local attempt

    for attempt in $(seq 1 $max_attempts); do
        if sudo apt-get update; then
            return 0
        fi

        if [[ $attempt -lt $max_attempts ]]; then
            sudo apt-get clean
            sudo rm -rf /var/lib/apt/lists/*
            sleep 5
        fi
    done

    return 1
}

install-prerequisites()
{
    echo
    echo "==========================="
    echo " Installing prerequisities"
    echo "==========================="
    echo

    # install mosquitto
    if ! which mosquitto; then
        apt-get-update-with-retry
        sudo apt-get install -y --no-install-recommends mosquitto-clients
    fi

    # install docker
    SYSTEM_NAME=$(uname -r)
    if ! [[ "$SYSTEM_NAME" == *"microsoft"* && "$SYSTEM_NAME" == *"WSL"* ]]; then
        if ! which docker;
        then
            apt-get-update-with-retry
            sudo apt-get install -y docker.io
        fi
    fi

    # install k3d
    if ! which k3d; then
        wget -q -O - https://raw.githubusercontent.com/k3d-io/k3d/main/install.sh | bash
    fi

    # install helm
    if ! which helm; then
        curl https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
    fi

    # install step
    if ! which step; then
        wget -q https://github.com/smallstep/cli/releases/download/v0.28.0/step-cli_amd64.deb -P /tmp
        sudo dpkg -i /tmp/step-cli_amd64.deb
    fi

    # install az cli
    if ! which az; then
        curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
        az aks install-cli
    fi

    # install/upgrade extensions
    az extension add --upgrade --name azure-iot-ops
    az extension add --upgrade --name connectedk8s

    # install k9s
    if ! which k9s; then
        wget -q https://github.com/derailed/k9s/releases/latest/download/k9s_linux_amd64.deb -P /tmp
        sudo dpkg -i /tmp/k9s_linux_amd64.deb
    fi
}

# Some k3d clusters come up with an API server that keeps answering 503 for
# minutes after 'k3d cluster create --wait' returns. Require the API server
# to stay healthy for a while, so an unstable cluster can be recreated.
wait-for-stable-api-server()
{
    local required_healthy_checks=15
    local check_interval_seconds=2
    local deadline=$((SECONDS + 180))
    local healthy_checks=0

    while ((SECONDS < deadline)); do
        if kubectl get --raw='/readyz' > /dev/null 2>&1 \
            && kubectl api-resources > /dev/null 2>&1; then
            healthy_checks=$((healthy_checks + 1))
        else
            healthy_checks=0
        fi

        if ((healthy_checks >= required_healthy_checks)); then
            if kubectl wait --for=condition=Available apiservice --all --timeout=60s; then
                return 0
            fi
            healthy_checks=0
        fi

        sleep $check_interval_seconds
    done

    echo "Kubernetes API server did not stay healthy for $((required_healthy_checks * check_interval_seconds)) seconds"
    kubectl get --raw='/readyz?verbose' || true
    docker logs --tail 200 k3d-k3s-default-server-0 || true
    return 1
}

create-cluster() {
    echo
    echo "========================="
    echo " Creating cluster"
    echo "========================="
    echo

    local max_attempts=3
    local attempt

    for attempt in $(seq 1 $max_attempts); do
        # Create k3d cluster and forwarded ports (MQTT/MQTTS)
        k3d cluster delete
        k3d registry delete k3d-registry.localhost > /dev/null 2>&1 || true
        if k3d cluster create \
            -p '1883:1883@loadbalancer' \
            -p '8883:8883@loadbalancer' \
            -p '8884:8884@loadbalancer' \
            --registry-create k3d-registry.localhost:127.0.0.1:5000 \
            --wait \
            && wait-for-stable-api-server; then
            break
        fi

        if ((attempt == max_attempts)); then
            echo "Failed to create a stable k3d cluster after $max_attempts attempts"
            return 1
        fi
        echo "k3d cluster is not stable, recreating it (attempt $((attempt + 1)) of $max_attempts)"
    done

    # Set the default context / namespace to azure-iot-operations
    kubectl config set-context k3d-k3s-default --namespace=azure-iot-operations

    echo
    echo "================================================================================================="
    echo " The k3d cluster has been created and the default context has been set to azure-iot-operations."
    echo
    echo " If you need non-root access to the cluster, run the following command:"
    echo
    echo " mkdir ~/.kube; sudo install -o $USER -g $USER -m 600 /root/.kube/config ~/.kube/config"
    echo "================================================================================================="
}

# Get the options
while getopts "hsy" option; do
    case $option in
        h) # display Help
            help
            exit;;
        s) # skip installation of prerequisites
            echo Skipping installation of prerequisites
            SKIP_INSTALL=true;;
        y) # assume yes to all questions
            AUTO_YES=true;;
    esac
done

script_dir=$(dirname $(readlink -f $0))

if [[ $SKIP_INSTALL != "true" ]]; then
    install-prerequisites
fi

if [[ $AUTO_YES != "true" ]]; then
    if [[ `k3d cluster list k3s-default` ]]; then
        echo
        read -p "An existing cluster was detected, are you sure you want to delete it? [y/N] " yn
        case $yn in
            [Yy]* ) ;;
            * ) exit 1;;
        esac
    fi
fi

create-cluster
