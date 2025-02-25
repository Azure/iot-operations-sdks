#!/bin/bash

set -o errexit # fail if any command fails

echo "Deploying Azure IoT Operations for development"

# setup some variables, and change into the script directory
script_dir=$(dirname $(readlink -f $0))
session_dir=$script_dir/../../.session
cd $script_dir
mkdir -p $session_dir

# Install the cert-man resources certificate not present
if ! kubectl get certificate/azure-iot-operations-aio-selfsigned &> /dev/null; then
    echo Missing certificate, installing...
    kubectl apply -f yaml/cert-man.yaml
fi

# create x509 certificate chain
step certificate create --profile root-ca "my root ca" \
    $session_dir/root_ca.crt $session_dir/root_ca.key \
    --no-password --insecure --force
step certificate create --profile intermediate-ca "my intermediate ca" \
    $session_dir/intermediate_ca.crt $session_dir/intermediate_ca.key \
    --ca $session_dir/root_ca.crt --ca-key $session_dir/root_ca.key \
    --no-password --insecure --force
step certificate create client $session_dir/client.crt $session_dir/client.key \
    --not-after 8760h \
    --ca $session_dir/intermediate_ca.crt \
    --ca-key $session_dir/intermediate_ca.key \
    --no-password --insecure --force

# create client trust bundle used to validate x509 client connections to the broker
kubectl delete configmap client-ca-trust-bundle -n azure-iot-operations --ignore-not-found
kubectl create configmap client-ca-trust-bundle -n azure-iot-operations \
    --from-literal=client_ca.pem="$(cat $session_dir/intermediate_ca.crt $session_dir/root_ca.crt)"

# Setup the MQTT broker
kubectl apply -f yaml/aio-developer.yaml

# Wait for MQTT broker trust bundle to be generated (for external connections to the MQTT Broker) and then push to a local file
kubectl wait --for=create --timeout=30s secret/azure-iot-operations-aio-ca-certificate -n cert-manager
kubectl get secret azure-iot-operations-aio-ca-certificate -n cert-manager -o jsonpath='{.data.ca\.crt}' | base64 -d > $session_dir/broker-ca.crt

# Create a SAT auth file for local testing
kubectl create token default --namespace azure-iot-operations --duration=86400s --audience=aio-internal > $session_dir/token.txt

echo Setup complete, session related files are in the '.session' directory
