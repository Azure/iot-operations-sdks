# Deploying MQ in codespaces 

tools/deployment/initialize-cluster.sh

kubectl create ns azure-iot-operations

# Install dependencies
helm repo add jetstack https://charts.jetstack.io --force-update
helm upgrade cert-manager jetstack/cert-manager --install --create-namespace -n cert-manager --version v1.16 --set crds.enabled=true --set extraArgs={--enable-certificate-owner-ref=true} --wait
helm upgrade trust-manager jetstack/trust-manager --install --create-namespace -n cert-manager --wait

# install MQTT broker
helm uninstall mq --ignore-not-found
helm uninstall broker --ignore-not-found
helm install mq --atomic --version 1.1.7 oci://mcr.microsoft.com/azureiotoperations/helm/aio-broker  -n azure-iot-operations --wait
kubectl wait --for=create --timeout=30s secret/azure-iot-operations-aio-ca-certificate -n cert-manager

# Setup the broker, helm doesn't install one by default
cat <<EOF | kubectl apply -f -
apiVersion: mqttbroker.iotoperations.azure.com/v1
kind: Broker
metadata:
  name: default
  namespace: azure-iot-operations
spec:
  generateResourceLimits:
    cpu: disabled
  cardinality:
    backendChain:
      partitions: 1
      redundancyFactor: 2
    frontend:
      replicas: 1
EOF


tools/deployment/configure-aio.sh


# Install the ADR CRDs and namespace
helm uninstall adr-crds-namespace --ignore-not-found
helm install adr-crds-namespace oci://azureadr.azurecr.io/helm/adr/common/adr-crds-prp --version 0.20.0-alpha.3


# Install the AKRI
helm uninstall akri --ignore-not-found
helm install akri oci://akribuilds.azurecr.io/helm/microsoft-managed-akri --version 0.8.0-20250529.1-pr -n azure-iot-operations \
    --set jobs.preUpgrade="false" \
    --set jobs.upgradeStatus="false"

# Wait for the akri CRDs to be installed before proceeding. Fail on timeout
echo "waiting for akri crds."

retry_interval=5
timeout_duration=180
start_time=$(date +%s)
elapsed_time=0

line_count=$(kubectl get crds | grep akri | wc -l)
while [ "$line_count" -ne 3 ] && [ "$elapsed_time" -lt "$timeout_duration" ]; do
    sleep $retry_interval

    line_count=$(kubectl get crds | grep akri | wc -l)

    # Calculate elapsed time
    current_time=$(date +%s)
    elapsed_time=$((current_time - start_time))
    echo "Elapsed time: $elapsed_time seconds"
done

if [ "$line_count" -eq 3 ]; then
    echo "Found 3 akri CRDs."
else
    echo "Error: Akri CRDs not found after $timeout_duration seconds."
    exit 1
fi

# Wait for operator to spin up before deploying any devices/assets
sleep 5

cd ./dotnet/samples/Connectors/SqlConnector/
./deploy-connector-and-device.sh 
cd ../../../..