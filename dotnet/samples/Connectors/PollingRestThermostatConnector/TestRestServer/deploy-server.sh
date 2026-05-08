#!/bin/bash

set -e

# Step 1: Generate certificates
./generate-v3-certs.sh

# Generate base64-encoded values from certificates
SERVER_CERT_B64=$(base64 -w 0 < certs/server-v3.crt)
SERVER_KEY_B64=$(base64 -w 0 < certs/server-v3.key)
CA_CERT_B64=$(base64 -w 0 < certs/ca-v3.crt)
INTERMEDIATE_CERT_B64=$(base64 -w 0 < certs/intermediate-v3.crt)

# Generate base64-encoded auth credentials (default values)
USERNAME_B64=$(echo -n "username" | base64)
PASSWORD_B64=$(echo -n "password" | base64)

kubectl delete secret generic rest-test-server-secrets --ignore-not-found --namespace=azure-iot-operations
kubectl create secret generic rest-test-server-secrets \
  --from-file=server.crt=certs/server-v3.crt \
  --from-file=server.key=certs/server-v3.key \
  --from-file=intermediate.crt=certs/intermediate-v3.crt \
  --from-file=ca.crt=certs/ca-v3.crt \
  --from-literal=username=username \
  --from-literal=password=password \
  --namespace=azure-iot-operations

kubectl delete secret generic rest-connector-trust-list --ignore-not-found --namespace=azure-iot-operations
kubectl create secret generic rest-connector-trust-list \
  --from-file=ca.crt=certs/ca-v3.crt \
  --namespace=azure-iot-operations

kubectl delete secret generic rest-x509-cert --ignore-not-found --namespace=azure-iot-operations
kubectl create secret generic rest-x509-cert \
  --from-file=certificate=certs/client-v3.crt \
  --from-file=intermediateCert=certs/intermediate-v3.crt \
  --from-file=key=certs/client-v3.key \
  --namespace=azure-iot-operations


# Step 4: Deploy the server
kubectl apply -f k8s-deployment.yaml --validate=false
