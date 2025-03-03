#!/bin/bash

set -o errexit # fail if any command fails

echo "Deploying Azure IoT Operations for development"

# setup some variables, and change into the script directory
script_dir=$(dirname $(readlink -f $0))
session_dir=$script_dir/../../.session
cd $script_dir
mkdir -p $session_dir

# Install the cert-man resources if certificate isn't present
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

# create client trust bundle used to validate x509 client connections to the broker
kubectl delete configmap client-ca-trust-bundle -n azure-iot-operations --ignore-not-found
kubectl create configmap client-ca-trust-bundle -n azure-iot-operations \
    --from-literal=client_ca.pem="$(cat $session_dir/intermediate_ca.crt $session_dir/root_ca.crt)"

# Setup the MQTT broker
kubectl apply -f yaml/aio-developer.yaml

# Create the credentials for auth to the MQTT broker
$script_dir/update-credentials.sh
