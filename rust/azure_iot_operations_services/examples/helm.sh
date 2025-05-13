#!/bin/bash
helm install akri oci://akribuilds.azurecr.io/helm/microsoft-managed-akri --version 0.8.0-20250418.8-pr -n azure-iot-operations
helm install adr-crds-namespace oci://azureadr.azurecr.io/helm/adr/common/adr-crds-prp --version 0.20.0-alpha.1