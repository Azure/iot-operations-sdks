#!/bin/bash

# Debugging: Print current directory
echo "Current directory: $(pwd)"

echo "Removing existing directory..."
rm -rf ./dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1

echo "Creating new directory..."
mkdir ./dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1

echo "Running ProtocolCompiler..."
../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile discovered_resources_commands_v1.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.Akri.Akri

# Debugging: Check if ProtocolCompiler executed successfully
if [ $? -eq 0 ]; then
    echo "ProtocolCompiler executed successfully."
else
    echo "Error: ProtocolCompiler failed to execute."
    exit 1
fi

# Debugging: List contents of temporary directory
echo "Contents of /tmp/Azure.Iot.Operations.Services.Akri.Akri:"
ls -R /tmp/Azure.Iot.Operations.Services.Akri.Akri

echo "Copying generated files..."
cp -f /tmp/Azure.Iot.Operations.Services.Akri.Akri/dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1/*.cs dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1 -v

# Debugging: Check if files were copied successfully
if [ $? -eq 0 ]; then
    echo "Files copied successfully."
else
    echo "Error: Failed to copy files."
    exit 1
fi

echo "Cleaning up temporary files..."
rm -rf /tmp/Azure.Iot.Operations.Services.Akri.Akri -v

# Debugging: Final check of the created directory
echo "Final contents of ./dtmi_ms_adr_Akri__1:"
ls -l ./dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1

echo "Script completed."


# rm -rf ./dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
# mkdir ./dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1
# ../../../../codegen/src/Azure.Iot.Operations.ProtocolCompiler/bin/Debug/net8.0/Azure.Iot.Operations.ProtocolCompiler --modelFile discovered_resources_commands_v1.json --lang csharp --outDir /tmp/Azure.Iot.Operations.Services.Akri.Akri
# cp -f /tmp/Azure.Iot.Operations.Services.Akri.Akri/dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1/*.cs dtmi_com_microsoft_deviceregistry_DiscoveredAssetResources__1 -v
# rm -rf /tmp/Azure.Iot.Operations.Services.Akri.Akri -v
