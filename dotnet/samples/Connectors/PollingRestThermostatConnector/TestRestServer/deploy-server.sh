#!/bin/bash
# deploy-server.sh - Deploy HTTP test server with certificates and secrets

set -e

# Determine script location and server type
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_NAME="http-server"

echo "🚀 Deploying $SERVER_NAME..."

# Step 1: Generate certificates
echo "📜 Generating X.509 v3 certificates..."
cd "$SCRIPT_DIR" || exit 1
if [ ! -f "./generate-v3-certs.sh" ]; then
    echo "❌ Error: generate-v3-certs.sh not found in $SCRIPT_DIR"
    exit 1
fi
./generate-v3-certs.sh

# Step 2: Create Kubernetes secret with generated certificates
echo "🔐 Creating Kubernetes secret..."

# Check kubectl connectivity first
echo "🔍 Checking Kubernetes cluster connectivity..."
if ! kubectl cluster-info >/dev/null 2>&1; then
    echo "❌ Error: Cannot connect to Kubernetes cluster"
    echo "   Please ensure your cluster is running and kubectl is configured"
    exit 1
fi

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

# Replace template values in k8s-secrets.yaml
sed -e "s/TODO_BASE64_ENCODED_SERVER_CERT/$SERVER_CERT_B64/" \
    -e "s/TODO_BASE64_ENCODED_SERVER_KEY/$SERVER_KEY_B64/" \
    -e "s/TODO_BASE64_ENCODED_CA_CERT/$CA_CERT_B64/" \
    -e "s/TODO_BASE64_ENCODED_INTERMEDIATE_CERT/$INTERMEDIATE_CERT_B64/" \
    -e "s/TODO_BASE64_ENCODED_USERNAME/$USERNAME_B64/" \
    -e "s/TODO_BASE64_ENCODED_PASSWORD/$PASSWORD_B64/" \
    k8s-secrets.yaml > "$TEMP_SECRETS"

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

echo "✅ $SERVER_NAME deployed successfully!"
echo ""
echo "🔍 Check deployment status:"
echo "   kubectl get pods -l app=rest-test-server"
echo "   kubectl get services -l app=rest-test-server"
echo ""
echo "� Check connector secrets:"
echo "   kubectl get secrets -n azure-iot-operations | grep rest-"
echo ""
echo "�📋 View logs:"
echo "   kubectl logs -l app=rest-test-server -f"
echo ""
echo "🌐 Port forwards for testing:"
echo "   kubectl port-forward service/rest-test-server 8080:8080 8081:8081 8443:8443 8444:8444 8445:8445"