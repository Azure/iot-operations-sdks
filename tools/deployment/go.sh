#!/bin/bash

set -o errexit # fail if any command fails

# Arc
az login
az connectedk8s connect --name $CLUSTER_NAME --location $LOCATION --resource-group $RESOURCE_GROUP
az connectedk8s enable-features -n $CLUSTER_NAME -g $RESOURCE_GROUP --features cluster-connect custom-locations

# Schema registry
az storage account create --name $STORAGE_ACCOUNT --location $LOCATION --resource-group $RESOURCE_GROUP --enable-hierarchical-namespace
az iot ops schema registry create --name $SCHEMA_REGISTRY --resource-group $RESOURCE_GROUP --registry-namespace $SCHEMA_REGISTRY_NAMESPACE --sa-resource-id $(az storage account show --name $STORAGE_ACCOUNT -o tsv --query id)

# Azure IoT Operations
az iot ops init --cluster $CLUSTER_NAME --resource-group $RESOURCE_GROUP
az iot ops create --cluster $CLUSTER_NAME --resource-group $RESOURCE_GROUP --name ${CLUSTER_NAME}-instance --sr-resource-id $(az iot ops schema registry show --name $SCHEMA_REGISTRY --resource-group $RESOURCE_GROUP -o tsv --query id) --fr 1 --fw 1 --bp 1 --bw 1 --br 2 --mp Low
