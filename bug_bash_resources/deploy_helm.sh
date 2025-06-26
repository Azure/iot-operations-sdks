#!/bin/sh

# Install ADR
helm uninstall adr-crds-namespace --ignore-not-found
helm install adr-crds-namespace \
    oci://azureadr.azurecr.io/helm/adr/common/adr-crds-prp --version 0.20.0-alpha.6 \
    --wait

# Install Akri
helm uninstall akri -n azure-iot-operations --ignore-not-found
helm install akri \
    oci://akribuilds.azurecr.io/helm/microsoft-managed-akri --version 0.8.0-20250611.2-pr \
    -n azure-iot-operations --set jobs.preUpgrade="false" --set jobs.upgradeStatus="false" \
    --wait
