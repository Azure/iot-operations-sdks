using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AzureDeviceRegistry
{
    public class AzureDeviceRegistryClientTests
    {

        [Fact]
        public void ConstructorLoadsFromEnvironmentSuccessfully()
        {
            SetupNormalEnvironmentVariables();

            // This would throw an exception if any of the expected environment variables aren't present.
            new AzureDeviceRegistryClient();
        }

        [Fact]
        public async Task GetAssetEndpointProfile()
        {
            SetupNormalEnvironmentVariables();

            var adrClient = new AzureDeviceRegistryClient();
            var assetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync();

            Assert.Equal(assetEndpointProfile.TargetAddress, File.ReadAllText("./AzureDeviceRegistry/testFiles/config/AEP_TARGET_ADDRESS"));
        }

        private void SetupNormalEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.ConfigMapMountPathEnvVar, "./AzureDeviceRegistry/testFiles/config");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepCertMountPathEnvVar, "./AzureDeviceRegistry/testFiles/aep_cert");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepUsernameSecretMountPathEnvVar, "./AzureDeviceRegistry/secret/aep_username");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepPasswordSecretMountPathEnvVar, "./AzureDeviceRegistry/secret/aep_password");
        }
    }
}
