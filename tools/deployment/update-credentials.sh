#!/bin/bash

set -o errexit # fail if any command fails

# setup some variables
session_dir=$(dirname $(readlink -f $0))/../../.session
mkdir -p $session_dir

# Wait for CA trust bundle to be generated (for external connections to the MQTT Broker) and then push to a local file
kubectl wait --for=create --timeout=30s secret/aio-broker-external-ca -n azure-iot-operations
kubectl get secret aio-broker-external-ca -n azure-iot-operations -o jsonpath='{.data.ca\.crt}' | base64 -d > $session_dir/broker-ca.crt

# create client certificate
step certificate create client $session_dir/client.crt $session_dir/client.key \
    -f \
    --not-after 8760h \
    --no-password \
    --insecure \
    --ca ~/.step/certs/intermediate_ca.crt \
    --ca-key ~/.step/secrets/intermediate_ca_key \
    --ca-password-file=$session_dir/password.txt

# Create a SAT auth file for local testing
kubectl create token default --namespace azure-iot-operations --duration=86400s --audience=aio-internal > $session_dir/token.txt