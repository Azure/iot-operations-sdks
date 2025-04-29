@description('A Unique name used for the Virtual Machine domain and also for generating resource names.')
param name string = ''

@description('Username for the Virtual Machine.')
param adminUsername string = ''

@description('Type of authentication to use on the Virtual Machine. SSH key is recommended.')
@allowed([
  'sshPublicKey'
  'password'
])
param authenticationType string = 'sshPublicKey'

@description('SSH key or password for the Virtual Machine. SSH key is recommended.')
@secure()
param adminPasswordOrKey string

@description('The size of the VM, at least 16GB RAM is required')
param vmSize string = 'Standard_E2a_v4'

// =========================
// == VARIABLES ==
// =========================

// General
var location = resourceGroup().location

// Virtual network
var addressPrefix = '10.1.0.0/16'
var subnetAddressPrefix = '10.1.0.0/24'
var dnsLabelPrefix = toLower('${name}-${uniqueString(resourceGroup().id)}')
var subnetName = '${uniqueString(dnsLabelPrefix)}-subnet'

// The resource id of the custom location
var customLocationId = resourceId('Microsoft.ManagedIdentity/systemAssignedIdentities', 'bc313c14-388c-4e7d-a58e-70017303ee3b')

// Virtual machine
var ubuntuOSVersion = {
    publisher: 'Canonical'
    offer: 'ubuntu-24_04-lts'
    sku: 'server'
    version: 'latest'
}
var osDiskType = 'Standard_LRS'
var linuxConfiguration = {
  disablePasswordAuthentication: true
  ssh: {
    publicKeys: [
      {
        path: '/home/${adminUsername}/.ssh/authorized_keys'
        keyData: adminPasswordOrKey
      }
    ]
  }
}
var cloudInitReplacements = [
  { key: '{{{location}}}',       value: location }
  { key: '{{{resource_group}}}', value: resourceGroup().name }
  { key: '{{{cluster_name}}}',   value: name }
  { key: '{{{custom_location_id}}}',   value: customLocationId }
]
var cloudInit = reduce(cloudInitReplacements, loadTextContent('cloud-init.yml'), (cur, next) => replace(string(cur), next.key, next.value))
var installScript = reduce(cloudInitReplacements, loadTextContent('install.sh'), (cur, next) => replace(string(cur), next.key, next.value))

// =========================
// == RESOURCES ==
// =========================
resource publicIPAddress 'Microsoft.Network/publicIPAddresses@2023-09-01' = {
  name: '${name}-ip'
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    publicIPAllocationMethod: 'Dynamic'
    publicIPAddressVersion: 'IPv4'
    dnsSettings: {
      domainNameLabel: name
    }
    idleTimeoutInMinutes: 4
  }
}

resource networkInterface 'Microsoft.Network/networkInterfaces@2023-09-01' = {
  name: '${name}-nic'
  location: location
  properties: {
    ipConfigurations: [
      {
        name: 'ipconfig1'
        properties: {
          subnet: {
            id: virtualNetwork.properties.subnets[0].id
          }
          privateIPAllocationMethod: 'Dynamic'
          publicIPAddress: {
            id: publicIPAddress.id
          }
        }
      }
    ]
    networkSecurityGroup: {
      id: networkSecurityGroup.id
    }
  }
}

resource networkSecurityGroup 'Microsoft.Network/networkSecurityGroups@2023-09-01' = {
  name: '${name}-nsg'
  location: location
  properties: {
    securityRules: [
      {
        name: 'SSH'
        properties: {
          priority: 1000
          protocol: 'Tcp'
          access: 'Allow'
          direction: 'Inbound'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '22'
        }
      }
    ]
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-09-01' = {
  name: '${name}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        addressPrefix
      ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          networkSecurityGroup: {
            id: networkSecurityGroup.id
          }
          addressPrefix: subnetAddressPrefix
          privateEndpointNetworkPolicies: 'Enabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
        }
      }
    ]
  }
}

resource vm 'Microsoft.Compute/virtualMachines@2023-09-01' = {
  name: name
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    hardwareProfile: {
      vmSize: vmSize
    }
    storageProfile: {
      osDisk: {
        createOption: 'FromImage'
        managedDisk: {
          storageAccountType: osDiskType
        }
      }
      imageReference: ubuntuOSVersion
    }
    networkProfile: {
      networkInterfaces: [
        {
          id: networkInterface.id
        }
      ]
    }
    osProfile: {
      computerName: name
      adminUsername: adminUsername
      adminPassword: adminPasswordOrKey
      linuxConfiguration: ((authenticationType == 'password') ? null : linuxConfiguration)
      customData: base64(cloudInit)
    }
  }
}

resource vmSetup 'Microsoft.Compute/virtualMachines/extensions@2019-03-01' = {
  name: name
  location: location
  parent: vm
  properties: {
    publisher: 'Microsoft.Azure.Extensions'
    type: 'CustomScript'
    typeHandlerVersion: '2.1'
    autoUpgradeMinorVersion: true
    settings: {
      script: base64(installScript)
    }
  }
}

// Create storage for the schema registry
// resource schemaStorage 'Microsoft.Storage/storageAccounts@2024-01-01' = {
//   name: '${name}storage'
//   location: location
//   kind: 'StorageV2'
//   sku: {
//     name: 'Standard_LRS'
//   }
//   properties:{
//     isHnsEnabled: true
//   }
// }

// // Create the schema registry
// resource schemaRegistry 'Microsoft.DeviceRegistry/schemaRegistries@2024-09-01-preview' = {
//   name: '${name}-schema'
//   location: location
//   properties: {
//     namespace: '${name}-schema-ns'
//     storageAccountContainerUrl: schemaStorage.properties.primaryEndpoints.blob
//   }
// }

// Give the VM Contributor access to the group has it can create the Arc resource
resource contributorRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  name: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
  scope: subscription()
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  // ISSUE: I don't think the vm.id is unique?
  name: guid(resourceGroup().id, vm.id, contributorRoleDefinition.id)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: contributorRoleDefinition.id
    principalId: vm.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Shutdown the VM at 1am to save costs
resource vmShutdown 'Microsoft.DevTestLab/schedules@2018-09-15' = {
  name: 'shutdown-computevm-${vm.name}'
  location: location
  properties: {
    targetResourceId: vm.id
    status: 'Enabled'
    taskType: 'ComputeVmShutdownTask'
    timeZoneId: 'Pacific Standard Time'
    dailyRecurrence: {
      time: '0100'
    }
  }  
}

// var customLocationId = resourceId('Microsoft.ExtendedLocation/customLocations', 'bc313c14-388c-4e7d-a58e-70017303ee3b')
// var connectedClusterId = resourceId('Microsoft.Kubernetes/connectedClusters', name)


// resource connectedCluster 'Microsoft.Kubernetes/ConnectedClusters@2024-01-01' = {
//   location: location
//   name: connectedClusterName
//   identity: {
//     type: 'SystemAssigned'
//   }
//   kind: 'ProvisionedCluster'
//   properties: {
//     // agentPublicKeyCertificate must be empty for provisioned clusters that will be created next.
//     agentPublicKeyCertificate: ''
//     aadProfile: {
//       enableAzureRBAC: false
//     }
//   }
// }

output adminUsername string = adminUsername
output hostname string = publicIPAddress.properties.dnsSettings.fqdn
output sshCommand string = 'ssh ${adminUsername}@${publicIPAddress.properties.dnsSettings.fqdn}'
