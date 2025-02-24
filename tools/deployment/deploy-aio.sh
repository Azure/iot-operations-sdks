#!/bin/bash

set -o errexit # fail if any command fails

echo "Deploying Azure IoT Operations for development"

# setup some variables, and change into the script directory
script_dir=$(dirname $(readlink -f $0))
session_dir=$script_dir/../../.session
mkdir -p $session_dir
cd $script_dir

# create root & intermediate CA
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

# Cleanup old Broker and create the new one
kubectl delete Broker --all
kubectl apply -f yaml/aio-developer.yaml

# Update the credentials locally for connecting to MQTT Broker
./update-credentials.sh

echo Setup complete, session related files are in the '.session' directory
