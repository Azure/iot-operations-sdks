using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System.Text;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AzureDeviceRegistry
{
    public class AzureDeviceRegistryClientTests
    {

        [Fact]
        public void ConstructorLoadsFromEnvironmentSuccessfully()
        {
            SetupTestEnvironment();

            // This would throw an exception if any of the expected environment variables aren't present.
            new AzureDeviceRegistryClient();
        }

        [Fact]
        public async Task GetAssetEndpointProfile()
        {
            SetupTestEnvironment();

            try
            {
                var adrClient = new AzureDeviceRegistryClient();
                var assetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync();

                Assert.Equal(File.ReadAllText("./AzureDeviceRegistry/testFiles/config/AEP_TARGET_ADDRESS"), assetEndpointProfile.TargetAddress);
                Assert.Equal(File.ReadAllText("./AzureDeviceRegistry/testFiles/config/AEP_AUTHENTICATION_METHOD"), assetEndpointProfile.AuthenticationMethod);
                Assert.Equal(File.ReadAllText("./AzureDeviceRegistry/testFiles/config/ENDPOINT_PROFILE_TYPE"), assetEndpointProfile.EndpointProfileType);
                Assert.NotNull(assetEndpointProfile.AdditionalConfiguration);
                Assert.True(assetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("DataSourceType", out var property));
                Assert.Equal(System.Text.Json.JsonValueKind.String, property.ValueKind);
                Assert.Equal("Http", property.GetString());

                Assert.NotNull(assetEndpointProfile.Credentials);
                Assert.NotNull(assetEndpointProfile.Credentials.Username);
                Assert.NotNull(assetEndpointProfile.Credentials.Password);

                Assert.Equal(File.ReadAllText("./AzureDeviceRegistry/testFiles/secret/aep_username/some-username"), assetEndpointProfile.Credentials.Username);
                Assert.Equal(File.ReadAllText("./AzureDeviceRegistry/testFiles/secret/aep_password/some-password"), Encoding.UTF8.GetString(assetEndpointProfile.Credentials.Password));
                Assert.Equal(File.ReadAllText("./AzureDeviceRegistry/testFiles/secret/aep_cert/some-certificate"), assetEndpointProfile.Credentials.Certificate);
            }
            finally
            {
                CleanupTestEnvironment();
            }
        }

        [Fact]
        public async Task ObserveAssetEndpointProfile()
        {
            SetupTestEnvironment();

            var adrClient = new AzureDeviceRegistryClient();
            try
            {
                var assetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync();

                TaskCompletionSource<AssetEndpointProfile> assetEndpointProfileTcs = new();
                adrClient.AssetEndpointProfileChanged += (sender, assetEndpointProfile) =>
                {
                    assetEndpointProfileTcs.TrySetResult(assetEndpointProfile);
                };

                await adrClient.ObserveAssetEndpointProfileAsync("someAssetId", TimeSpan.FromMilliseconds(1000));

                string expectedNewTargetAddress = Guid.NewGuid().ToString();
                File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_TARGET_ADDRESS", expectedNewTargetAddress);
                var updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewTargetAddress, updatedAssetEndpointProfile.TargetAddress);

                assetEndpointProfileTcs = new();
                string expectedNewAuthenticationMethod = Guid.NewGuid().ToString();
                File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_AUTHENTICATION_METHOD", expectedNewAuthenticationMethod);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewAuthenticationMethod, updatedAssetEndpointProfile.AuthenticationMethod);
                Assert.Equal(expectedNewTargetAddress, updatedAssetEndpointProfile.TargetAddress);

                assetEndpointProfileTcs = new();
                string expectedNewEndpointProfileType = Guid.NewGuid().ToString();
                File.WriteAllText("./AzureDeviceRegistry/testFiles/config/ENDPOINT_PROFILE_TYPE", expectedNewEndpointProfileType);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewEndpointProfileType, updatedAssetEndpointProfile.EndpointProfileType);
                Assert.Equal(expectedNewAuthenticationMethod, updatedAssetEndpointProfile.AuthenticationMethod);
                Assert.Equal(expectedNewTargetAddress, updatedAssetEndpointProfile.TargetAddress);

                assetEndpointProfileTcs = new();
                string expectedNewDataSourceType = Guid.NewGuid().ToString();
                string expectedNewAdditionalConfiguration = "{ \"DataSourceType\": \"" + expectedNewDataSourceType + "\" }";
                File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_ADDITIONAL_CONFIGURATION", expectedNewAdditionalConfiguration);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(updatedAssetEndpointProfile.AdditionalConfiguration);
                Assert.True(updatedAssetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("DataSourceType", out var property));
                Assert.Equal(System.Text.Json.JsonValueKind.String, property.ValueKind);
                Assert.Equal(expectedNewDataSourceType, property.GetString());

                assetEndpointProfileTcs = new();
                string expectedNewCertValue = Guid.NewGuid().ToString();
                File.WriteAllText($"./AzureDeviceRegistry/testFiles/secret/aep_cert/some-certificate", expectedNewCertValue);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(updatedAssetEndpointProfile.Credentials);
                Assert.Equal(expectedNewCertValue, updatedAssetEndpointProfile.Credentials.Certificate);


                assetEndpointProfileTcs = new();
                string expectedNewUsernameValue = Guid.NewGuid().ToString();
                File.WriteAllText($"./AzureDeviceRegistry/testFiles/secret/aep_username/some-username", expectedNewUsernameValue);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(updatedAssetEndpointProfile.Credentials);
                Assert.Equal(expectedNewUsernameValue, updatedAssetEndpointProfile.Credentials.Username);


                assetEndpointProfileTcs = new();
                string expectedNewPasswordValue = Guid.NewGuid().ToString();
                File.WriteAllText($"./AzureDeviceRegistry/testFiles/secret/aep_password/some-password", expectedNewPasswordValue);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(updatedAssetEndpointProfile.Credentials);
                Assert.NotNull(updatedAssetEndpointProfile.Credentials.Password);
                Assert.Equal(expectedNewPasswordValue, Encoding.UTF8.GetString(updatedAssetEndpointProfile.Credentials.Password));
            }
            finally
            {
                await adrClient.UnobserveAssetEndpointProfileAsync("someAssetId");
                CleanupTestEnvironment();
            }
        }

        [Fact]
        public async Task CanObserveAssetEndpointProfileAfterUnobserve()
        {
            SetupTestEnvironment();

            var adrClient = new AzureDeviceRegistryClient();
            try
            {
                var assetEndpointProfile = await adrClient.GetAssetEndpointProfileAsync();

                TaskCompletionSource<AssetEndpointProfile> assetEndpointProfileTcs = new();
                adrClient.AssetEndpointProfileChanged += (sender, assetEndpointProfile) =>
                {
                    assetEndpointProfileTcs.TrySetResult(assetEndpointProfile);
                };

                await adrClient.ObserveAssetEndpointProfileAsync("someAssetId", TimeSpan.FromMilliseconds(1000));

                string expectedNewTargetAddress = Guid.NewGuid().ToString();
                File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_TARGET_ADDRESS", expectedNewTargetAddress);
                var updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewTargetAddress, updatedAssetEndpointProfile.TargetAddress);

                await adrClient.UnobserveAssetEndpointProfileAsync("someAssetId");

                await adrClient.ObserveAssetEndpointProfileAsync("someAssetId", TimeSpan.FromMilliseconds(1000));

                assetEndpointProfileTcs = new();
                string expectedNewTargetAddress2 = Guid.NewGuid().ToString();
                File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_TARGET_ADDRESS", expectedNewTargetAddress2);
                var updatedAssetEndpointProfile2 = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewTargetAddress2, updatedAssetEndpointProfile2.TargetAddress);

            }
            finally
            {
                await adrClient.UnobserveAssetEndpointProfileAsync("someAssetId");
                CleanupTestEnvironment();
            }
        }

        private void SetupTestEnvironment()
        {
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AssetEndpointProfileConfigMapMountPathEnvVar, "./AzureDeviceRegistry/testFiles/config");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepCertMountPathEnvVar, "./AzureDeviceRegistry/testFiles/secret/aep_cert");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepUsernameSecretMountPathEnvVar, "./AzureDeviceRegistry/testFiles/secret/aep_username");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepPasswordSecretMountPathEnvVar, "./AzureDeviceRegistry/testFiles/secret/aep_password");

            ResetFileContents();

            // These files are required for the test runs to work
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/config/AEP_TARGET_ADDRESS"));
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/config/AEP_AUTHENTICATION_METHOD"));
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/config/ENDPOINT_PROFILE_TYPE"));
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/config/AEP_CERT_FILE_NAME"));
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/config/AEP_USERNAME_FILE_NAME"));
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/config/AEP_PASSWORD_FILE_NAME"));
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/secret/aep_username/some-username"));
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/secret/aep_password/some-password"));
            Assert.True(File.Exists("./AzureDeviceRegistry/testFiles/secret/aep_cert/some-certificate"));
        }

        // Some tests write changes to the test files, but we don't want to have to manually revert those changes later. This method
        // should always run after any such test.
        private void ResetFileContents()
        {
            if (!Directory.Exists("./AzureDeviceRegistry/testFiles/config"))
            { 
                Directory.CreateDirectory("./AzureDeviceRegistry/testFiles/config");
            }

            if (!Directory.Exists("./AzureDeviceRegistry/testFiles/secret"))
            {
                Directory.CreateDirectory("./AzureDeviceRegistry/testFiles/secret");
                Directory.CreateDirectory("./AzureDeviceRegistry/testFiles/secret/aep_username");
                Directory.CreateDirectory("./AzureDeviceRegistry/testFiles/secret/aep_password");
                Directory.CreateDirectory("./AzureDeviceRegistry/testFiles/secret/aep_cert");
            }

            File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_TARGET_ADDRESS", "http://my-backend-api-s.default.svc.cluster.local:80");
            File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_ADDITIONAL_CONFIGURATION", "{ \"DataSourceType\": \"Http\" }");
            File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_AUTHENTICATION_METHOD", "UsernamePassword");
            File.WriteAllText("./AzureDeviceRegistry/testFiles/config/ENDPOINT_PROFILE_TYPE", "http-sql-dssroot");
            File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_CERT_FILE_NAME", "some-certificate");
            File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_USERNAME_FILE_NAME", "some-username");
            File.WriteAllText("./AzureDeviceRegistry/testFiles/config/AEP_PASSWORD_FILE_NAME", "some-password");
            File.WriteAllText("./AzureDeviceRegistry/testFiles/secret/aep_username/some-username", "myusername");
            File.WriteAllText("./AzureDeviceRegistry/testFiles/secret/aep_password/some-password", "mypassword");
            File.WriteAllText(
                "./AzureDeviceRegistry/testFiles/secret/aep_cert/some-certificate",
                "-----BEGIN CERTIFICATE-----\r\nMIICEjCCAXsCAg36MA0GCSqGSIb3DQEBBQUAMIGbMQswCQYDVQQGEwJKUDEOMAwG\r\nA1UECBMFVG9reW8xEDAOBgNVBAcTB0NodW8ta3UxETAPBgNVBAoTCEZyYW5rNERE\r\nMRgwFgYDVQQLEw9XZWJDZXJ0IFN1cHBvcnQxGDAWBgNVBAMTD0ZyYW5rNEREIFdl\r\nYiBDQTEjMCEGCSqGSIb3DQEJARYUc3VwcG9ydEBmcmFuazRkZC5jb20wHhcNMTIw\r\nODIyMDUyNjU0WhcNMTcwODIxMDUyNjU0WjBKMQswCQYDVQQGEwJKUDEOMAwGA1UE\r\nCAwFVG9reW8xETAPBgNVBAoMCEZyYW5rNEREMRgwFgYDVQQDDA93d3cuZXhhbXBs\r\nZS5jb20wXDANBgkqhkiG9w0BAQEFAANLADBIAkEAm/xmkHmEQrurE/0re/jeFRLl\r\n8ZPjBop7uLHhnia7lQG/5zDtZIUC3RVpqDSwBuw/NTweGyuP+o8AG98HxqxTBwID\r\nAQABMA0GCSqGSIb3DQEBBQUAA4GBABS2TLuBeTPmcaTaUW/LCB2NYOy8GMdzR1mx\r\n8iBIu2H6/E2tiY3RIevV2OW61qY2/XRQg7YPxx3ffeUugX9F4J/iPnnu1zAxxyBy\r\n2VguKv4SWjRFoRkIfIlHX0qVviMhSlNy2ioFLy7JcPZb+v3ftDGywUqcBiVDoea0\r\nHn+GmxZA\r\n-----END CERTIFICATE-----\r\n");
        }

        private void CleanupTestEnvironment()
        {
            File.Delete("./AzureDeviceRegistry/testFiles/config/AEP_TARGET_ADDRESS");
            File.Delete("./AzureDeviceRegistry/testFiles/config/AEP_ADDITIONAL_CONFIGURATION");
            File.Delete("./AzureDeviceRegistry/testFiles/config/AEP_AUTHENTICATION_METHOD");
            File.Delete("./AzureDeviceRegistry/testFiles/config/ENDPOINT_PROFILE_TYPE");
            File.Delete("./AzureDeviceRegistry/testFiles/config/AEP_CERT_FILE_NAME");
            File.Delete("./AzureDeviceRegistry/testFiles/config/AEP_USERNAME_FILE_NAME");
            File.Delete("./AzureDeviceRegistry/testFiles/config/AEP_PASSWORD_FILE_NAME");
            File.Delete("./AzureDeviceRegistry/testFiles/secret/aep_username/some-username");
            File.Delete("./AzureDeviceRegistry/testFiles/secret/aep_password/some-password");
            File.Delete("./AzureDeviceRegistry/testFiles/secret/aep_cert/some-certificate");
        }
    }
}
