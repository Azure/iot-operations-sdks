#!/bin/bash

set -o errexit # fail if any command fails

# check input args
if [[ -z "$1" ]] || ! [[ "$1" =~ ^(nightly|release)$ ]]; then
    echo "Error: Missing argument"
    echo "  Options are 'nightly' or 'release'"
    exit 1
fi

# change to .devcontainer directory
cd $(dirname $(readlink -f $0))

# add/upgrade the azure-iot-ops extension
az extension add --upgrade --name azure-iot-ops

# Install Jetstack helm repository
helm repo add jetstack https://charts.jetstack.io --force-update
helm repo update

if [ "$1" = "nightly" ]; then
    echo Installing Nightly Mqtt Broker

    # install cert-manager
    helm upgrade cert-manager jetstack/cert-manager --install --create-namespace --version v1.13 --set installCRDs=true --set extraArgs={--enable-certificate-owner-ref=true} --wait

    # install MQTT Broker
    helm install mq --atomic oci://mqbuilds.azurecr.io/helm/mq --version 0.6.0-nightly --create-namespace -n azure-iot-operations
fi

# clean up any existing pieces we dont want
kubectl delete configmap client-ca-trust-bundle -n azure-iot-operations --ignore-not-found
kubectl delete BrokerAuthentication -n azure-iot-operations --all
kubectl delete BrokerListener -n azure-iot-operations --all
kubectl delete Broker -n azure-iot-operations --all
rm -f ../client_ca.pem ../client.crt ../client.key ~/password.txt
rm -rf ~/.step

# install trust-manager with azure-iot-operations as the trusted domain
helm upgrade trust-manager jetstack/trust-manager --install --create-namespace -n azure-iot-operations --set app.trust.namespace=azure-iot-operations --wait

# install cert issuers and trust bundle
kubectl apply -f ./yaml/certificates.yaml

# Wait for CA trust bundle to be generated for external connections to the MQTT Broker and then add to the local env
while ! kubectl get secret aio-mq-broker-external-ca -n azure-iot-operations; do
    echo "Waiting for ca."
    sleep 5
done
kubectl get secret aio-mq-broker-external-ca -n azure-iot-operations -o jsonpath='{.data.ca\.crt}' | base64 -d >../mqtt-broker-ca.crt

# create CA for client connections. This will not be used directly by a service so many of the fields are not applicable
echo "my-ca-password" > /tmp/password.txt
step ca init \
    --deployment-type=standalone \
    --name=my-ca \
    --password-file=/tmp/password.txt \
    --address=:0 \
    --dns=notapplicable \
    --provisioner=notapplicable

# create client certificate
step certificate create client ../client.crt ../client.key \
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

# setup new Broker
if [ "$1" = "nightly" ]; then
    kubectl apply -f ./yaml/aio-nightly.yaml
else
    kubectl apply -f ./yaml/aio-release.yaml
fi
