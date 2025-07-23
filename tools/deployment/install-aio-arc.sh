#!/bin/bash

set -o errexit # fail if any command fails

# Usage info
# LOCATION: An Azure region close to you. For the list of currently Azure IoT Operations supported regions, see https://learn.microsoft.com/azure/iot-operations/overview-iot-operations#supported-regions.
# RESOURCE_GROUP: A name for a new Azure resource group where your cluster will be created.
# CLUSTER_NAME: A name for your Kubernetes cluster.
# STORAGE_ACCOUNT: A name for your storage account. Must be between 3 and 24 characters, and only contain numbers and lowercase letters.
# SCHEMA_REGISTRY: A name for your schema registry. Can only contain numbers, lowercase letters, and hyphens.
# SCHEMA_REGISTRY_NAMESPACE: A name for your schema registry namespace. Uniquely identifies a schema registry within a tenant. Can only contain numbers, lowercase letters, and hyphens.

usage() {
    echo "Usage: $0 [-l location] [-g resource_group] [-c cluster_name] [-s storage_account] [-r schema_registry] [-n schema_registry_namespace]"
    exit 1
}

# Parse arguments
while getopts "l:g:c:s:r:n:" opt; do
  case $opt in
    l) LOCATION="$OPTARG" ;;
    g) RESOURCE_GROUP="$OPTARG" ;;
    c) CLUSTER_NAME="$OPTARG" ;;
    s) STORAGE_ACCOUNT="$OPTARG" ;;
    r) SCHEMA_REGISTRY="$OPTARG" ;;
    n) SCHEMA_REGISTRY_NAMESPACE="$OPTARG" ;;
    *) usage ;;
  esac
done

# check if the required environment variables are set
if [ -z $LOCATION ]; then echo "LOCATION is not set"; exit 1; fi
if [ -z $RESOURCE_GROUP ]; then echo "RESOURCE_GROUP is not set"; exit 1; fi
if [ -z $CLUSTER_NAME ]; then echo "CLUSTER_NAME is not set"; exit 1; fi
if [ -z $STORAGE_ACCOUNT ]; then echo "STORAGE_ACCOUNT is not set"; exit 1; fi
if [ -z $SCHEMA_REGISTRY ]; then echo "SCHEMA_REGISTRY is not set"; exit 1; fi
if [ -z $SCHEMA_REGISTRY_NAMESPACE ]; then echo "SCHEMA_REGISTRY_NAMESPACE is not set"; exit 1; fi

# login if needed
if ! az account show; then
    az login
fi

# create a resource group
echo ===Creating Resource Group===
az group create --name $RESOURCE_GROUP --location $LOCATION

# install providers
echo ===Registering Providers===
az provider register -n "Microsoft.ExtendedLocation"
az provider register -n "Microsoft.Kubernetes"
az provider register -n "Microsoft.KubernetesConfiguration"
az provider register -n "Microsoft.IoTOperations"
az provider register -n "Microsoft.DeviceRegistry"
az provider register -n "Microsoft.SecretSyncController"

# install arc
echo ===Installing Azure Arc===
az connectedk8s connect --name $CLUSTER_NAME --location $LOCATION --resource-group $RESOURCE_GROUP
echo ===Enabling Azure Arc Features===
#az connectedk8s enable-features -n $CLUSTER_NAME -g $RESOURCE_GROUP --features cluster-connect custom-locations
export OBJECT_ID=$(az ad sp show --id bc313c14-388c-4e7d-a58e-70017303ee3b --query id -o tsv)
az connectedk8s enable-features -n $CLUSTER_NAME -g $RESOURCE_GROUP --custom-locations-oid $OBJECT_ID --features cluster-connect custom-locations

# install schema registry
echo ===Creating Storage Account===
az storage account create --name $STORAGE_ACCOUNT --location $LOCATION --resource-group $RESOURCE_GROUP --enable-hierarchical-namespace
echo ===Creating Schema Registry===
az iot ops schema registry create --name $SCHEMA_REGISTRY --resource-group $RESOURCE_GROUP --registry-namespace $SCHEMA_REGISTRY_NAMESPACE --sa-resource-id $(az storage account show --name $STORAGE_ACCOUNT -o tsv --query id)

# install azure iot operations
echo ===Initializing Azure IoT Operations===
az iot ops init --cluster $CLUSTER_NAME --resource-group $RESOURCE_GROUP
echo ===Creating Azure IoT Operations===
az iot ops create --cluster $CLUSTER_NAME --resource-group $RESOURCE_GROUP --name ${CLUSTER_NAME}-instance  --sr-resource-id $(az iot ops schema registry show --name $SCHEMA_REGISTRY --resource-group $RESOURCE_GROUP -o tsv --query id) --broker-frontend-replicas 1 --broker-frontend-workers 1  --broker-backend-part 1  --broker-backend-workers 1 --broker-backend-rf 2 --broker-mem-profile Low
