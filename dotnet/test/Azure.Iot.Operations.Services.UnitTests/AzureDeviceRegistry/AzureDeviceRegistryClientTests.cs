using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System.Text;
using System.Text.Json;
using Xunit;
using Xunit.Sdk;

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

                Assert.Equal(File.ReadAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepTargetAddressRelativeMountPath}"), assetEndpointProfile.TargetAddress);
                Assert.Equal(File.ReadAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepAuthenticationMethodRelativeMountPath}"), assetEndpointProfile.AuthenticationMethod);
                Assert.Equal(File.ReadAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.EndpointProfileTypeRelativeMountPath}"), assetEndpointProfile.EndpointProfileType);
                Assert.NotNull(assetEndpointProfile.AdditionalConfiguration);
                Assert.True(assetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("DataSourceType", out var property));
                Assert.Equal(System.Text.Json.JsonValueKind.String, property.ValueKind);
                Assert.Equal("Http", property.GetString());

                Assert.NotNull(assetEndpointProfile.Credentials);
                Assert.NotNull(assetEndpointProfile.Credentials.Username);
                Assert.NotNull(assetEndpointProfile.Credentials.Password);

                Assert.Equal(File.ReadAllText("./AzureDeviceRegistryTestFiles/secret/aep_username/some-username"), assetEndpointProfile.Credentials.Username);
                Assert.Equal(File.ReadAllText("./AzureDeviceRegistryTestFiles/secret/aep_password/some-password"), Encoding.UTF8.GetString(assetEndpointProfile.Credentials.Password));
                Assert.Equal(File.ReadAllText("./AzureDeviceRegistryTestFiles/secret/aep_cert/some-certificate"), assetEndpointProfile.Credentials.Certificate);
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
                adrClient.AssetEndpointProfileChanged += (sender, args) =>
                {
                    assetEndpointProfileTcs.TrySetResult(args.AssetEndpointProfile!);
                };

                await adrClient.ObserveAssetEndpointProfileAsync(TimeSpan.FromMilliseconds(100));

                string expectedNewTargetAddress = Guid.NewGuid().ToString();
                File.WriteAllText("./AzureDeviceRegistryTestFiles/config/aep_config/AEP_TARGET_ADDRESS", expectedNewTargetAddress);
                var updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewTargetAddress, updatedAssetEndpointProfile.TargetAddress);

                assetEndpointProfileTcs = new();
                string expectedNewAuthenticationMethod = Guid.NewGuid().ToString();
                File.WriteAllText("./AzureDeviceRegistryTestFiles/config/aep_config/AEP_AUTHENTICATION_METHOD", expectedNewAuthenticationMethod);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewAuthenticationMethod, updatedAssetEndpointProfile.AuthenticationMethod);
                Assert.Equal(expectedNewTargetAddress, updatedAssetEndpointProfile.TargetAddress);

                assetEndpointProfileTcs = new();
                string expectedNewEndpointProfileType = Guid.NewGuid().ToString();
                File.WriteAllText("./AzureDeviceRegistryTestFiles/config/aep_config/ENDPOINT_PROFILE_TYPE", expectedNewEndpointProfileType);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewEndpointProfileType, updatedAssetEndpointProfile.EndpointProfileType);
                Assert.Equal(expectedNewAuthenticationMethod, updatedAssetEndpointProfile.AuthenticationMethod);
                Assert.Equal(expectedNewTargetAddress, updatedAssetEndpointProfile.TargetAddress);

                assetEndpointProfileTcs = new();
                string expectedNewDataSourceType = Guid.NewGuid().ToString();
                string expectedNewAdditionalConfiguration = "{ \"DataSourceType\": \"" + expectedNewDataSourceType + "\" }";
                File.WriteAllText("./AzureDeviceRegistryTestFiles/config/aep_config/AEP_ADDITIONAL_CONFIGURATION", expectedNewAdditionalConfiguration);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(updatedAssetEndpointProfile.AdditionalConfiguration);
                Assert.True(updatedAssetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("DataSourceType", out var property));
                Assert.Equal(JsonValueKind.String, property.ValueKind);
                Assert.Equal(expectedNewDataSourceType, property.GetString());

                assetEndpointProfileTcs = new();
                string expectedNewCertValue = Guid.NewGuid().ToString();
                File.WriteAllText($"./AzureDeviceRegistryTestFiles/secret/aep_cert/some-certificate", expectedNewCertValue);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(updatedAssetEndpointProfile.Credentials);
                Assert.Equal(expectedNewCertValue, updatedAssetEndpointProfile.Credentials.Certificate);


                assetEndpointProfileTcs = new();
                string expectedNewUsernameValue = Guid.NewGuid().ToString();
                File.WriteAllText($"./AzureDeviceRegistryTestFiles/secret/aep_username/some-username", expectedNewUsernameValue);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(updatedAssetEndpointProfile.Credentials);
                Assert.Equal(expectedNewUsernameValue, updatedAssetEndpointProfile.Credentials.Username);


                assetEndpointProfileTcs = new();
                string expectedNewPasswordValue = Guid.NewGuid().ToString();
                File.WriteAllText($"./AzureDeviceRegistryTestFiles/secret/aep_password/some-password", expectedNewPasswordValue);
                updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.NotNull(updatedAssetEndpointProfile.Credentials);
                Assert.NotNull(updatedAssetEndpointProfile.Credentials.Password);
                Assert.Equal(expectedNewPasswordValue, Encoding.UTF8.GetString(updatedAssetEndpointProfile.Credentials.Password));
            }
            finally
            {
                await adrClient.UnobserveAssetEndpointProfileAsync();
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
                adrClient.AssetEndpointProfileChanged += (sender, args) =>
                {
                    assetEndpointProfileTcs.TrySetResult(args.AssetEndpointProfile!);
                };

                await adrClient.ObserveAssetEndpointProfileAsync(TimeSpan.FromMilliseconds(100));

                string expectedNewTargetAddress = Guid.NewGuid().ToString();
                File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepTargetAddressRelativeMountPath}", expectedNewTargetAddress);
                var updatedAssetEndpointProfile = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewTargetAddress, updatedAssetEndpointProfile.TargetAddress);

                await adrClient.UnobserveAssetEndpointProfileAsync();

                await adrClient.ObserveAssetEndpointProfileAsync(TimeSpan.FromMilliseconds(100));

                assetEndpointProfileTcs = new();
                string expectedNewTargetAddress2 = Guid.NewGuid().ToString();
                File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepTargetAddressRelativeMountPath}", expectedNewTargetAddress2);
                var updatedAssetEndpointProfile2 = await assetEndpointProfileTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedNewTargetAddress2, updatedAssetEndpointProfile2.TargetAddress);

            }
            finally
            {
                await adrClient.UnobserveAssetEndpointProfileAsync();
                CleanupTestEnvironment();
            }
        }

        [Fact]
        public async Task ObserveAsset_NoStartingAsset()
        {
            SetupTestEnvironment();

            var adrClient = new AzureDeviceRegistryClient();
            try
            {
                TaskCompletionSource<AssetChangedEventArgs> assetTcs = new();
                adrClient.AssetChanged += (sender, args) =>
                {
                    assetTcs.TrySetResult(args);
                };

                await adrClient.ObserveAssetsAsync(TimeSpan.FromMilliseconds(100));

                Asset testAsset = new Asset()
                {
                    Datasets =
                    [
                        new Dataset()
                        {
                            DataPoints =
                            [
                                new DataPoint()
                                { 
                                    DataSource = "someDatasource",
                                    Name = "someDatapoint"
                                },
                                new DataPoint()
                                {
                                    DataSource = "someOtherDatasource",
                                    Name = "someOtherDatapoint"
                                }
                            ],
                            Name = "someDataset"
                        }
                    ],
                    DefaultTopic = new Topic()
                    {
                        Path = "somePath",
                        Retain = RetainHandling.Never,
                    },
                };

                string testAssetName = Guid.NewGuid().ToString();
                AddOrUpdateAssetToEnvironment(testAssetName, testAsset);

                var assetChangeEventArgs = await assetTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Equal(ChangeType.Created, assetChangeEventArgs.ChangeType);
                Asset? observedAsset = assetChangeEventArgs.Asset;
                Assert.NotNull(observedAsset);
                Assert.NotNull(observedAsset.DefaultTopic);
                Assert.Equal(testAsset.DefaultTopic.Path, observedAsset.DefaultTopic.Path);

                assetTcs = new();

                RemoveAssetFromEnvironment(testAssetName);

                var assetChangeEventArgs2 = await assetTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(ChangeType.Deleted, assetChangeEventArgs2.ChangeType);
                Assert.Null(assetChangeEventArgs2.Asset);
            }
            finally
            {
                await adrClient.UnobserveAssetsAsync();
                CleanupTestEnvironment();
            }
        }

        [Fact]
        public async Task ObserveAsset_WithStartingAsset()
        {
            SetupTestEnvironment();

            var adrClient = new AzureDeviceRegistryClient();
            try
            {
                Asset testAsset = new Asset()
                {
                    Datasets =
                    [
                        new Dataset()
                        {
                            DataPoints =
                            [
                                new DataPoint()
                                {
                                    DataSource = "someDatasource",
                                    Name = "someDatapoint"
                                },
                                new DataPoint()
                                {
                                    DataSource = "someOtherDatasource",
                                    Name = "someOtherDatapoint"
                                }
                            ],
                            Name = "someDataset"
                        }
                    ],
                    DefaultTopic = new Topic()
                    {
                        Path = "somePath",
                        Retain = RetainHandling.Never,
                    },
                };

                string testAssetName = Guid.NewGuid().ToString();
                AddOrUpdateAssetToEnvironment(testAssetName, testAsset);

                TaskCompletionSource<AssetChangedEventArgs> assetTcs = new();
                adrClient.AssetChanged += (sender, args) =>
                {
                    assetTcs.TrySetResult(args);
                };

                await adrClient.ObserveAssetsAsync(TimeSpan.FromMilliseconds(100));

                string newTopicPath = Guid.NewGuid().ToString();
                testAsset.DefaultTopic.Path = newTopicPath;
                AddOrUpdateAssetToEnvironment(testAssetName, testAsset);

                var assetChangeEventArgs = await assetTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

                Assert.Equal(ChangeType.Updated, assetChangeEventArgs.ChangeType);
                Asset? observedAsset = assetChangeEventArgs.Asset;
                Assert.NotNull(observedAsset);
                Assert.NotNull(observedAsset.DefaultTopic);
                Assert.Equal(newTopicPath, observedAsset.DefaultTopic.Path);
            }
            finally
            {
                await adrClient.UnobserveAssetsAsync();
                CleanupTestEnvironment();
            }
        }

        private void SetupTestEnvironment()
        {
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AssetEndpointProfileConfigMapMountPathEnvVar, "./AzureDeviceRegistryTestFiles/config/aep_config");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepCertMountPathEnvVar, "./AzureDeviceRegistryTestFiles/secret/aep_cert");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepUsernameSecretMountPathEnvVar, "./AzureDeviceRegistryTestFiles/secret/aep_username");
            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AepPasswordSecretMountPathEnvVar, "./AzureDeviceRegistryTestFiles/secret/aep_password");

            ResetFileContents();

            // These files are required for the test runs to work
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepTargetAddressRelativeMountPath}"));
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepAuthenticationMethodRelativeMountPath}"));
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.EndpointProfileTypeRelativeMountPath}"));
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepCertificateFileNameRelativeMountPath}"));
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepUsernameFileNameRelativeMountPath}"));
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepPasswordFileNameRelativeMountPath}"));
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/secret/aep_username/some-username"));
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/secret/aep_password/some-password"));
            Assert.True(File.Exists($"./AzureDeviceRegistryTestFiles/secret/aep_cert/some-certificate"));
        }

        private void AddOrUpdateAssetToEnvironment(string assetName, Asset asset)
        {
            string assetJson = JsonSerializer.Serialize(asset);

            Environment.SetEnvironmentVariable(AzureDeviceRegistryClient.AssetConfigMapMountPathEnvVar, "./AzureDeviceRegistryTestFiles/config/asset_config");

            if (!Directory.Exists("./AzureDeviceRegistryTestFiles/config/asset_config"))
            {
                Directory.CreateDirectory("./AzureDeviceRegistryTestFiles/config/asset_config");
            }

            string fileName = $"./AzureDeviceRegistryTestFiles/config/asset_config/{assetName}";
            File.WriteAllText(fileName, assetJson);
        }

        private void RemoveAssetFromEnvironment(string assetName)
        {
            if (File.Exists($"./AzureDeviceRegistryTestFiles/config/asset_config/{assetName}"))
            {
                File.Delete($"./AzureDeviceRegistryTestFiles/config/asset_config/{assetName}");
            }
        }

        // Some tests write changes to the test files, but we don't want to have to manually revert those changes later. This method
        // should always run after any such test.
        private void ResetFileContents()
        {
            if (!Directory.Exists("./AzureDeviceRegistryTestFiles/config/aep_config"))
            { 
                Directory.CreateDirectory("./AzureDeviceRegistryTestFiles/config/aep_config");
            }

            if (!Directory.Exists("./AzureDeviceRegistryTestFiles/secret"))
            {
                Directory.CreateDirectory("./AzureDeviceRegistryTestFiles/secret");
                Directory.CreateDirectory("./AzureDeviceRegistryTestFiles/secret/aep_username");
                Directory.CreateDirectory("./AzureDeviceRegistryTestFiles/secret/aep_password");
                Directory.CreateDirectory("./AzureDeviceRegistryTestFiles/secret/aep_cert");
            }

            File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepTargetAddressRelativeMountPath}", "http://my-backend-api-s.default.svc.cluster.local:80");
            File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepAdditionalConfigurationRelativeMountPath}", "{ \"DataSourceType\": \"Http\" }");
            File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepAuthenticationMethodRelativeMountPath}", "UsernamePassword");
            File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.EndpointProfileTypeRelativeMountPath}", "http-sql-dssroot");
            File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepCertificateFileNameRelativeMountPath}", "some-certificate");
            File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepUsernameFileNameRelativeMountPath}", "some-username");
            File.WriteAllText($"./AzureDeviceRegistryTestFiles/config/aep_config/{AzureDeviceRegistryClient.AepPasswordFileNameRelativeMountPath}", "some-password");
            File.WriteAllText($"./AzureDeviceRegistryTestFiles/secret/aep_username/some-username", "myusername");
            File.WriteAllText($"./AzureDeviceRegistryTestFiles/secret/aep_password/some-password", "mypassword");
            File.WriteAllText(
                "./AzureDeviceRegistryTestFiles/secret/aep_cert/some-certificate",
                "-----BEGIN CERTIFICATE-----\r\nMIICEjCCAXsCAg36MA0GCSqGSIb3DQEBBQUAMIGbMQswCQYDVQQGEwJKUDEOMAwG\r\nA1UECBMFVG9reW8xEDAOBgNVBAcTB0NodW8ta3UxETAPBgNVBAoTCEZyYW5rNERE\r\nMRgwFgYDVQQLEw9XZWJDZXJ0IFN1cHBvcnQxGDAWBgNVBAMTD0ZyYW5rNEREIFdl\r\nYiBDQTEjMCEGCSqGSIb3DQEJARYUc3VwcG9ydEBmcmFuazRkZC5jb20wHhcNMTIw\r\nODIyMDUyNjU0WhcNMTcwODIxMDUyNjU0WjBKMQswCQYDVQQGEwJKUDEOMAwGA1UE\r\nCAwFVG9reW8xETAPBgNVBAoMCEZyYW5rNEREMRgwFgYDVQQDDA93d3cuZXhhbXBs\r\nZS5jb20wXDANBgkqhkiG9w0BAQEFAANLADBIAkEAm/xmkHmEQrurE/0re/jeFRLl\r\n8ZPjBop7uLHhnia7lQG/5zDtZIUC3RVpqDSwBuw/NTweGyuP+o8AG98HxqxTBwID\r\nAQABMA0GCSqGSIb3DQEBBQUAA4GBABS2TLuBeTPmcaTaUW/LCB2NYOy8GMdzR1mx\r\n8iBIu2H6/E2tiY3RIevV2OW61qY2/XRQg7YPxx3ffeUugX9F4J/iPnnu1zAxxyBy\r\n2VguKv4SWjRFoRkIfIlHX0qVviMhSlNy2ioFLy7JcPZb+v3ftDGywUqcBiVDoea0\r\nHn+GmxZA\r\n-----END CERTIFICATE-----\r\n");
        }

        private void CleanupTestEnvironment()
        {
            if (Directory.Exists("./AzureDeviceRegistryTestFiles/"))
            {
                Directory.Delete($"./AzureDeviceRegistryTestFiles/", true);
            }
        }
    }
}
