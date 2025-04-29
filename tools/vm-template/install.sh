#!/bin/sh

# set -o errexit # fail if any command fails

MYUSER=`id -un 1000`
MYHOME=`eval echo ~$MYUSER`
MYWORK=/workspaces
LOCATION={{{location}}}
RESOURCE_GROUP={{{resource_group}}}
CLUSTER_NAME={{{cluster_name}}}
CUSTOM_LOCATION_ID={{{custom_location_id}}}

mkdir -p $MYWORK
cd $MYWORK

STORAGE_ACCOUNT=${CLUSTER_NAME}storage
SCHEMA_REGISTRY=${CLUSTER_NAME}-schema
SCHEMA_REGISTRY_NAMESPACE=${CLUSTER_NAME}-schema-ns

echo
echo "+-----------------------------------------------------------+"
echo " Setting up the environment with the following parameters:"
echo "   Location           = ${LOCATION}"   
echo "   Resource Group     = ${RESOURCE_GROUP}"
echo "   Cluster Name       = ${CLUSTER_NAME}"
echo "   Custom Location ID = ${CUSTOM_LOCATION_ID}"
echo "+-----------------------------------------------------------+"
echo

echo
echo "+------------------------------------+"
echo "| Waiting for cloud-init completion  |"
echo "+------------------------------------+"
cloud-init status --wait

# echo
# echo "========================================="
# echo "=== Cloning repository ==="
# echo "========================================="
# git clone https://github.com/azure/iot-operations-sdks

echo
echo "+------------------------------------+"
echo "| Install pre-requisites             |"
echo "+------------------------------------+"
curl -s https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
curl -s https://raw.githubusercontent.com/rancher/k3d/main/install.sh | bash
wget -q https://github.com/derailed/k9s/releases/latest/download/k9s_linux_amd64.deb && dpkg -i ./k9s_linux_amd64.deb

echo
echo "+------------------------------------+"
echo "| Creating k8s cluster               |"
echo "+------------------------------------+"
k3d cluster create \
    -p '1883:1883@loadbalancer' \
    -p '8883:8883@loadbalancer' \
    -p '8884:8884@loadbalancer' \
    --wait

# Set the default context / namespace to azure-iot-operations
kubectl config set-context k3d-k3s-default --namespace=azure-iot-operations

echo
echo "+------------------------------------+"
echo "| Configuring user access to cluster |"
echo "+------------------------------------+"
install -D -o $MYUSER -g $MYUSER -m 600 $MYWORK/.kube/config $MYHOME/.kube/config
chown -R $MYUSER:$MYUSER $MYWORK

# echo
# echo "========================================="
# echo "=== Logging in to Azure ==="
# echo "========================================="
# az login --identity

# echo
# echo "========================================="
# echo "=== Registering Azure providers ==="
# echo "========================================="
# Issue #1: Cant register providers with system identity
# az provider register -n "Microsoft.ExtendedLocation"
# az provider register -n "Microsoft.Kubernetes"
# az provider register -n "Microsoft.KubernetesConfiguration"
# az provider register -n "Microsoft.IoTOperations"
# az provider register -n "Microsoft.DeviceRegistry"
# az provider register -n "Microsoft.SecretSyncController"

echo
echo "+------------------------------------+"
echo "| Arc connecting the cluster         |"
echo "+------------------------------------+"
az connectedk8s connect --name $CLUSTER_NAME --location $LOCATION -g $RESOURCE_GROUP --kube-config .kube/config

echo
echo "+------------------------------------+"
echo "| Enabling custom locations          |"
echo "+------------------------------------+"
az connectedk8s enable-features -n $CLUSTER_NAME -g $RESOURCE_GROUP --custom-locations-oid $CUSTOM_LOCATION_ID --kube-config .kube/config --features cluster-connect custom-locations

echo
echo "+------------------------------------+"
echo "| Creating Schema Registry resource  |"
echo "+------------------------------------+"
az storage account create --name $STORAGE_ACCOUNT --location $LOCATION -g $RESOURCE_GROUP --enable-hierarchical-namespace
az iot ops schema registry create --name $SCHEMA_REGISTRY -g $RESOURCE_GROUP --registry-namespace $SCHEMA_REGISTRY_NAMESPACE --sa-resource-id $(az storage account show --name $STORAGE_ACCOUNT -o tsv --query id)

# echo
# echo "========================================="
# echo "=== Installing IoT Operations ==="
# echo "========================================="
# az iot ops init --cluster $CLUSTER_NAME -g $RESOURCE_GROUP
# az iot ops create --cluster $CLUSTER_NAME -g $RESOURCE_GROUP --name ${CLUSTER_NAME}-instance --sr-resource-id $(az iot ops schema registry show --name $SCHEMA_REGISTRY -g $RESOURCE_GROUP -o tsv --query id) --broker-frontend-replicas 1 --broker-frontend-workers 1  --broker-backend-part 1  --broker-backend-workers 1 --broker-backend-rf 2 --broker-mem-profile Low
