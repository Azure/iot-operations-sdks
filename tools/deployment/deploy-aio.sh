#!/bin/bash

set -o errexit # fail if any command fails

# check input args
deploy_type=$1
if [[ -z "$deploy_type" ]] || ! [[ "$deploy_type" =~ ^(nightly|release)$ ]]; then
    echo "Error: Missing argument"
    echo "  Options are 'nightly' or 'release'"
    echo "  Example: './deploy-aio.sh nightly'"
    exit 1
fi

echo "Installing $deploy_type build of MQTT Broker"

# setup some variables, and change into the script directory
script_dir=$(dirname $(readlink -f $0))
session_dir=$script_dir/../../.session
mkdir -p $session_dir
cd $script_dir

# add & upgrade the extensions
az extension add --upgrade --name azure-iot-ops
az extension add --upgrade --name connectedk8s

# If its a nightly build, we need to install all the dependencies
if [ "$deploy_type" = "nightly" ]; then
    # Install Jetstack helm repository
    helm repo add jetstack https://charts.jetstack.io --force-update

    # install cert-manager
    helm upgrade cert-manager jetstack/cert-manager --install --create-namespace -n cert-manager --version v1.16 --set crds.enabled=true --set extraArgs={--enable-certificate-owner-ref=true} --wait

    # install trust-manager
    helm upgrade trust-manager jetstack/trust-manager --install --create-namespace -n cert-manager --wait

    # install MQTT broker
    helm uninstall broker -n azure-iot-operations --ignore-not-found
    helm install broker --atomic --create-namespace -n azure-iot-operations --version 1.1.0-dev oci://mqbuilds.azurecr.io/helm/aio-broker --wait

    # add ADR
    helm install adr --version 1.0.0 oci://mcr.microsoft.com/azureiotoperations/helm/adr/assets-arc-extension

    # add Akri service, port 18883
    helm install akri oci://mcr.microsoft.com/azureiotoperations/helm/microsoft-managed-akri --version 0.6.1 \
        --set agent.extensionService.mqttBroker.useTls=true \
        --set agent.extensionService.mqttBroker.caCertConfigMapRef=azure-iot-operations-aio-ca-trust-bundle \
        --set agent.extensionService.mqttBroker.authenticationMethod=serviceAccountToken \
        --set agent.extensionService.mqttBroker.hostName=aio-broker \
        --set agent.extensionService.mqttBroker.port=18883 \
        -n azure-iot-operations

    # deploy the Akri Operator
    helm install akri-operator oci://akripreview.azurecr.io/helm/microsoft-managed-akri-operator --version 0.1.5-preview -n azure-iot-operations

fi

# create root & intermediate CA
step certificate create --profile root-ca "my root ca" \
    $session_dir/root_ca.crt $session_dir/root_ca.key \
    --no-password --insecure
step certificate create --profile intermediate-ca "my intermediate ca" \
    $session_dir/intermediate_ca.crt $session_dir/intermediate_ca.key \
    --ca $session_dir/root_ca.crt --ca-key $session_dir/root_ca.key \
    --no-password --insecure

# create client trust bundle used to validate x509 client connections to the broker
kubectl delete configmap client-ca-trust-bundle -n azure-iot-operations --ignore-not-found
kubectl create configmap client-ca-trust-bundle -n azure-iot-operations \
    --from-literal=client_ca.pem="$(cat $session_dir/intermediate_ca.crt $session_dir/root_ca.crt)"

# setup new Broker
kubectl apply -f yaml/aio-$deploy_type.yaml

# Update the credentials locally for connecting to MQTT Broker
./update-credentials.sh

echo Setup complete, session related files are in the '.session' directory
