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

# add/upgrade the azure-iot-ops extension
az extension add --upgrade --name azure-iot-ops

# Install Jetstack helm repository
helm repo add jetstack https://charts.jetstack.io --force-update
helm repo update

if [ "$deploy_type" = "nightly" ]; then
    # install cert-manager
    helm upgrade cert-manager jetstack/cert-manager --install --create-namespace --version v1.13 --set installCRDs=true --set extraArgs={--enable-certificate-owner-ref=true} --wait

    # install AIO Broker
    helm uninstall broker --ignore-not-found
    helm install broker --atomic --create-namespace -n default --version 0.7.0-nightly oci://mqbuilds.azurecr.io/helm/aio-broker --wait
fi

# clean up any deployed Broker pieces
kubectl delete configmap client-ca-trust-bundle -n default --ignore-not-found
kubectl delete BrokerAuthentication -n default --all
kubectl delete BrokerListener -n default --all
kubectl delete Broker -n default --all

# install trust-manager with default as the trusted domain
helm upgrade trust-manager jetstack/trust-manager --install --create-namespace -n default --set app.trust.namespace=default --wait

# install cert issuers and trust bundle
kubectl apply -f yaml/certificates.yaml

# Wait for CA trust bundle to be generated (for external connections to the MQTT Broker) and then push to a local file
kubectl wait --for=create --timeout=30s secret/aio-broker-external-ca -n default
kubectl get secret aio-broker-external-ca -n default -o jsonpath='{.data.ca\.crt}' | base64 -d > $session_dir/broker-ca.crt

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
step certificate create client $session_dir/client.crt $session_dir/client.key \
    -f \
    --not-after 8760h \
    --no-password \
    --insecure \
    --ca ~/.step/certs/intermediate_ca.crt \
    --ca-key ~/.step/secrets/intermediate_ca_key \
    --ca-password-file=/tmp/password.txt

# create client trust bundle used to validate x509 client connections to the broker
kubectl create configmap client-ca-trust-bundle \
    -n default \
    --from-literal=client_ca.pem="$(cat ~/.step/certs/intermediate_ca.crt ~/.step/certs/root_ca.crt)"

# Create a SAT auth file for local testing
kubectl create token default --namespace default --duration=86400s --audience=aio-internal > $session_dir/token.txt

# setup new Broker
kubectl apply -f yaml/aio-$deploy_type.yaml

echo Setup complete, session related files are in the '.session' directory
