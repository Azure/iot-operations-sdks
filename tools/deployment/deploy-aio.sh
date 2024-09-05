#!/bin/bash

set -o errexit # fail if any command fails

# check input args
if [[ -z "$1" ]] || ! [[ "$1" =~ ^(nightly|release)$ ]]; then
    echo "Error: Missing argument"
    echo "  Options are 'nightly' or 'release'"
    echo "  Example: './deploy-aio.sh nightly'"
    exit 1
fi

echo "Installing $1 build"

# change to deploy script directory
cd $(dirname $(readlink -f $0))

# add/upgrade the azure-iot-ops extension
az extension add --upgrade --name azure-iot-ops

# Install Jetstack helm repository
helm repo add jetstack https://charts.jetstack.io --force-update
helm repo update

if [ "$1" = "nightly" ]; then
    # install cert-manager
    helm upgrade cert-manager jetstack/cert-manager --install --create-namespace --version v1.13 --set installCRDs=true --set extraArgs={--enable-certificate-owner-ref=true} --wait

    # install AIO Broker
    helm uninstall broker --ignore-not-found
    helm install broker --atomic --create-namespace -n azure-iot-operations --version 0.7.0-nightly oci://mqbuilds.azurecr.io/helm/aio-broker --wait
fi

# clean up any deployed Broker pieces
kubectl delete configmap client-ca-trust-bundle -n azure-iot-operations --ignore-not-found
kubectl delete BrokerAuthentication -n azure-iot-operations --all
kubectl delete BrokerListener -n azure-iot-operations --all
kubectl delete Broker -n azure-iot-operations --all

# install trust-manager with azure-iot-operations as the trusted domain
helm upgrade trust-manager jetstack/trust-manager --install --create-namespace -n azure-iot-operations --set app.trust.namespace=azure-iot-operations --wait

# install cert issuers and trust bundle
kubectl apply -f yaml/certificates.yaml

# Wait for CA trust bundle to be generated for external connections to the MQTT Broker and then push to a local file
while ! kubectl get secret aio-broker-external-ca -n azure-iot-operations; do
    echo "Waiting for broker ca..."
    sleep 2
done
kubectl get secret aio-broker-external-ca -n azure-iot-operations -o jsonpath='{.data.ca\.crt}' | base64 -d >../../broker-ca.crt

# create CA for client connections. This will not be used directly by a service so many of the fields are not applicable
echo "my-ca-password" > /tmp/password.txt
rm -rf ~/.step
step ca init \
    --deployment-type=standalone \
    --name=my-ca \
    --password-file=/tmp/password.txt \
    --address=:0 \
    --dns=notapplicable \
    --provisioner=notapplicable

# create client certificate
step certificate create client ../../client.crt ../../client.key \
    -f \
    --not-after 8760h \
    --no-password \
    --insecure \
    --ca ~/.step/certs/intermediate_ca.crt \
    --ca-key ~/.step/secrets/intermediate_ca_key \
    --ca-password-file=/tmp/password.txt

# create client trust bundle used to validate x509 client connections to the broker
kubectl create configmap client-ca-trust-bundle \
    -n azure-iot-operations \
    --from-literal=client_ca.pem="$(cat ~/.step/certs/intermediate_ca.crt ~/.step/certs/root_ca.crt)"

# Create a SAT auth file testing
kubectl create token default --namespace azure-iot-operations --duration=86400s --audience=aio-internal > ../../token.txt

# setup new Broker
kubectl apply -f yaml/aio-${1}.yaml
