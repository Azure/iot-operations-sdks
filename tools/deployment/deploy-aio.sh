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

# add/upgrade the azure-iot-ops / connectedk8s extensions
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

    # install AIO Broker
    helm uninstall broker -n azure-iot-operations --ignore-not-found
    helm install broker --atomic --create-namespace -n azure-iot-operations --version 0.7.0-nightly oci://mqbuilds.azurecr.io/helm/aio-broker --wait
fi

# create CA for client connections. This will not be used directly by a service so many of the fields are not applicable
echo "my-ca-password" > $session_dir/password.txt
rm -rf ~/.step
step ca init \
    --deployment-type=standalone \
    --name=my-ca \
    --password-file=$session_dir/password.txt \
    --address=:0 \
    --dns=notapplicable \
    --provisioner=notapplicable

# create client trust bundle used to validate x509 client connections to the broker
kubectl delete configmap client-ca-trust-bundle -n azure-iot-operations --ignore-not-found
kubectl create configmap client-ca-trust-bundle \
    -n azure-iot-operations \
    --from-literal=client_ca.pem="$(cat ~/.step/certs/intermediate_ca.crt ~/.step/certs/root_ca.crt)"

# setup new Broker
kubectl apply -f yaml/aio-$deploy_type.yaml

# Update the credentials locally for connecting to MQTT Broker
./update-credentials.sh

# Add ADR
helm install adr --version 0.2.0 oci://mcr.microsoft.com/azureiotoperations/helm/adr/assets-arc-extension -n azure-iot-operations --wait

# Deploy the Akri Operator
helm install akri-operator oci://akribuilds.azurecr.io/helm/microsoft-managed-akri-operator --version 0.4.0-main-20241101.1-buddy -n azure-iot-operations --wait

# Add AKRI Service for 38884
helm install akri oci://mcr.microsoft.com/azureiotoperations/helm/microsoft-managed-akri --version 0.5.8 \
--set agent.extensionService.mqttBroker.useTls=true \
--set agent.extensionService.mqttBroker.caCertConfigMapRef="azure-iot-operations-aio-ca-trust-bundle" \
--set agent.extensionService.mqttBroker.authenticationMethod=serviceAccountToken \
--set agent.extensionService.mqttBroker.hostName=aio-broker-external.azure-iot-operations.svc.cluster.local \
--set agent.extensionService.mqttBroker.port=38884 \
-n azure-iot-operations --wait

echo Setup complete, session related files are in the '.session' directory
