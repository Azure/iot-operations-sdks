#!/bin/bash

set -e

# Step 1: Generate certificates
echo "📜 Generating X.509 v3 certificates..."
./generate-v3-certs.sh

# Generate base64-encoded values from certificates
SERVER_CERT_B64=$(base64 -w 0 < certs/server-v3.crt)
SERVER_KEY_B64=$(base64 -w 0 < certs/server-v3.key)
CA_CERT_B64=$(base64 -w 0 < certs/ca-v3.crt)
INTERMEDIATE_CERT_B64=$(base64 -w 0 < certs/intermediate-v3.crt)

# Generate base64-encoded auth credentials (default values)
USERNAME_B64=$(echo -n "username" | base64)
PASSWORD_B64=$(echo -n "password" | base64)

# Create temporary secrets file with actual values
SECRET_NAME="rest-test-server-secrets"
TEMP_SECRETS="./temp-${SECRET_NAME}.yaml"

# Apply the secret
kubectl apply -f "$TEMP_SECRETS" --validate=false
rm "$TEMP_SECRETS"

# Step 3: Create connector secrets for azure-iot-operations namespace
echo "🔑 Creating connector secrets for azure-iot-operations namespace..."

# Create REST connector secrets
cat <<EOF | kubectl apply -f - --validate=false
apiVersion: v1
kind: Secret
metadata:
  name: rest-up
  namespace: azure-iot-operations
type: Opaque
data:
  passwordKey: $PASSWORD_B64
  usernameKey: $USERNAME_B64
---
apiVersion: v1
kind: Secret
metadata:
  name: rest-trust-list
  namespace: azure-iot-operations
type: Opaque
data:
  ca.crt: $CA_CERT_B64
---
apiVersion: v1
kind: Secret
metadata:
  name: rest-x509-cert
  namespace: azure-iot-operations
type: Opaque
data:
  key: $(base64 -w 0 < certs/client-v3.key)
  intermediateCert: $INTERMEDIATE_CERT_B64
  certificate: $(base64 -w 0 < certs/client-v3.crt)
EOF

# Step 4: Deploy the server
echo "⚡ Deploying server to Kubernetes..."
kubectl apply -f k8s-deployment.yaml --validate=false
