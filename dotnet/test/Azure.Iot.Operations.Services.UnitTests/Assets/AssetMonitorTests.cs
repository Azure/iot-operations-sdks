// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using Xunit;

namespace Azure.Iot.Operations.Services.Assets.UnitTests
{
    public class AssetMonitorTests
    {
        [Fact]
        public async Task GetAssetEndpointProfile()
        {
            SetupTestEnvironment();

            try
            {
                var assetMonitor = new AssetMonitor();
                var assetEndpointProfile = await assetMonitor.GetAssetEndpointProfileAsync();

                Assert.Equal(File.ReadAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepTargetAddressRelativeMountPath}"), assetEndpointProfile.TargetAddress);
                Assert.Equal(File.ReadAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepAuthenticationMethodRelativeMountPath}"), assetEndpointProfile.AuthenticationMethod);
                Assert.Equal(File.ReadAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.EndpointProfileTypeRelativeMountPath}"), assetEndpointProfile.EndpointProfileType);
                Assert.NotNull(assetEndpointProfile.AdditionalConfiguration);
                Assert.True(assetEndpointProfile.AdditionalConfiguration.RootElement.TryGetProperty("DataSourceType", out var property));
                Assert.Equal(JsonValueKind.String, property.ValueKind);
                Assert.Equal("Http", property.GetString());

                Assert.NotNull(assetEndpointProfile.Credentials);
                Assert.NotNull(assetEndpointProfile.Credentials.Username);
                Assert.NotNull(assetEndpointProfile.Credentials.Password);

                Assert.Equal(File.ReadAllText("./AssetMonitorTestFiles/secret/aep_username/some-username"), assetEndpointProfile.Credentials.Username);
                Assert.Equal(File.ReadAllText("./AssetMonitorTestFiles/secret/aep_password/some-password"), Encoding.UTF8.GetString(assetEndpointProfile.Credentials.Password));
                Assert.Equal(File.ReadAllText("./AssetMonitorTestFiles/secret/aep_cert/some-certificate"), assetEndpointProfile.Credentials.Certificate);
            }
            finally
            {
                CleanupTestEnvironment();
            }
        }

        [Fact]
        public async Task GetAsset()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task GetAssetNames()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task ObserveAsset_NoStartingAsset()
        {
            SetupTestEnvironment();

            var assetMonitor = new AssetMonitor();
            try
            {
                AssetChangedEventArgs? latestAssetState = null;
                assetMonitor.AssetChanged += (sender, args) =>
                {
                    latestAssetState = args;
                };

                assetMonitor.ObserveAssets(TimeSpan.FromMilliseconds(100));

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

                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (latestAssetState == null || latestAssetState.ChangeType != ChangeType.Created)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }

                Assert.Equal(ChangeType.Created, latestAssetState.ChangeType);
                Asset? observedAsset = latestAssetState.Asset;
                Assert.NotNull(observedAsset);
                Assert.NotNull(observedAsset.DefaultTopic);
                Assert.Equal(testAsset.DefaultTopic.Path, observedAsset.DefaultTopic.Path);

                RemoveAssetFromEnvironment(testAssetName);

                while (latestAssetState == null || latestAssetState.ChangeType != ChangeType.Deleted)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }

                Assert.Equal(ChangeType.Deleted, latestAssetState.ChangeType);
                Assert.Null(latestAssetState.Asset);

                // Test that unobserving assets stops any notifications from being sent. Test this by creating the asset
                // again and waiting for a bit to see if the client sends any notifications (it shouldn't).
                assetMonitor.UnobserveAssets();

                AddOrUpdateAssetToEnvironment(testAssetName, testAsset);
                CancellationTokenSource cancellationTokenSource2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                while (latestAssetState.ChangeType != ChangeType.Created)
                {
                    if (cancellationTokenSource2.IsCancellationRequested)
                    {
                        // The asset monitor did not receive any notifications about the asset being created as expected,
                        // so exit the test gracefully.
                        return;
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }
            finally
            {
                assetMonitor.UnobserveAssets();
                CleanupTestEnvironment();
            }
        }

        [Fact]
        public async Task ObserveAsset_WithStartingAsset()
        {
            SetupTestEnvironment();

            var assetMonitor = new AssetMonitor();
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

                AssetChangedEventArgs? latestAssetState = null;
                assetMonitor.AssetChanged += (sender, args) =>
                {
                    latestAssetState = args;
                };

                assetMonitor.ObserveAssets(TimeSpan.FromMilliseconds(100));

                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                while (latestAssetState == null || latestAssetState.ChangeType != ChangeType.Created)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }

                string newTopicPath = Guid.NewGuid().ToString();
                testAsset.DefaultTopic.Path = newTopicPath;
                AddOrUpdateAssetToEnvironment(testAssetName, testAsset);

                while (latestAssetState == null || latestAssetState.ChangeType != ChangeType.Updated)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }

                Assert.Equal(ChangeType.Updated, latestAssetState.ChangeType);
                Asset? observedAsset = latestAssetState.Asset;
                Assert.NotNull(observedAsset);
                Assert.NotNull(observedAsset.DefaultTopic);
                Assert.Equal(newTopicPath, observedAsset.DefaultTopic.Path);

                RemoveAssetFromEnvironment(testAssetName);

                while (latestAssetState == null || latestAssetState.ChangeType != ChangeType.Deleted)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }

                Assert.Equal(ChangeType.Deleted, latestAssetState.ChangeType);
                Assert.Null(latestAssetState.Asset);

                // Test that unobserving assets stops any notifications from being sent. Test this by creating the asset
                // again and waiting for a bit to see if the client sends any notifications (it shouldn't).
                assetMonitor.UnobserveAssets();

                AddOrUpdateAssetToEnvironment(testAssetName, testAsset);
                CancellationTokenSource cancellationTokenSource2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                while (latestAssetState.ChangeType != ChangeType.Created)
                {
                    if (cancellationTokenSource2.IsCancellationRequested)
                    {
                        // The asset monitor did not receive any notifications about the asset being created as expected,
                        // so exit the test gracefully.
                        return;
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }
            finally
            {
                assetMonitor.UnobserveAssets();
                CleanupTestEnvironment();
            }
        }

        [Fact]
        public async Task ObserveAssetEndpointProfile_NoStartingAssetEndpointProfile()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task ObserveAssetEndpointProfile_WithStartingAssetEndpointProfile()
        {
            throw new NotImplementedException();
        }

        private void SetupTestEnvironment()
        {
            Environment.SetEnvironmentVariable(AssetMonitor.AssetEndpointProfileConfigMapMountPathEnvVar, "./AssetMonitorTestFiles/config/aep_config");
            Environment.SetEnvironmentVariable(AssetMonitor.AepCertMountPathEnvVar, "./AssetMonitorTestFiles/secret/aep_cert");
            Environment.SetEnvironmentVariable(AssetMonitor.AepUsernameSecretMountPathEnvVar, "./AssetMonitorTestFiles/secret/aep_username");
            Environment.SetEnvironmentVariable(AssetMonitor.AepPasswordSecretMountPathEnvVar, "./AssetMonitorTestFiles/secret/aep_password");

            ResetFileContents();

            // These files are required for the test runs to work
            Assert.True(File.Exists($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepTargetAddressRelativeMountPath}"));
            Assert.True(File.Exists($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepAuthenticationMethodRelativeMountPath}"));
            Assert.True(File.Exists($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.EndpointProfileTypeRelativeMountPath}"));
            Assert.True(File.Exists($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepCertificateFileNameRelativeMountPath}"));
            Assert.True(File.Exists($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepUsernameFileNameRelativeMountPath}"));
            Assert.True(File.Exists($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepPasswordFileNameRelativeMountPath}"));
            Assert.True(File.Exists($"./AssetMonitorTestFiles/secret/aep_username/some-username"));
            Assert.True(File.Exists($"./AssetMonitorTestFiles/secret/aep_password/some-password"));
            Assert.True(File.Exists($"./AssetMonitorTestFiles/secret/aep_cert/some-certificate"));
        }

        private void AddOrUpdateAssetToEnvironment(string assetName, Asset asset)
        {
            string assetJson = JsonSerializer.Serialize(asset);

            Environment.SetEnvironmentVariable(AssetMonitor.AssetConfigMapMountPathEnvVar, "./AssetMonitorTestFiles/config/asset_config");

            if (!Directory.Exists("./AssetMonitorTestFiles/config/asset_config"))
            {
                Directory.CreateDirectory("./AssetMonitorTestFiles/config/asset_config");
            }

            string fileName = $"./AssetMonitorTestFiles/config/asset_config/{assetName}";
            File.WriteAllText(fileName, assetJson);
        }

        private void RemoveAssetFromEnvironment(string assetName)
        {
            if (File.Exists($"./AssetMonitorTestFiles/config/asset_config/{assetName}"))
            {
                File.Delete($"./AssetMonitorTestFiles/config/asset_config/{assetName}");
            }
        }

        // Some tests write changes to the test files, but we don't want to have to manually revert those changes later. This method
        // should always run after any such test.
        private void ResetFileContents()
        {
            if (!Directory.Exists("./AssetMonitorTestFiles/config/aep_config"))
            { 
                Directory.CreateDirectory("./AssetMonitorTestFiles/config/aep_config");
            }

            if (!Directory.Exists("./AssetMonitorTestFiles/secret"))
            {
                Directory.CreateDirectory("./AssetMonitorTestFiles/secret");
                Directory.CreateDirectory("./AssetMonitorTestFiles/secret/aep_username");
                Directory.CreateDirectory("./AssetMonitorTestFiles/secret/aep_password");
                Directory.CreateDirectory("./AssetMonitorTestFiles/secret/aep_cert");
            }

            File.WriteAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepTargetAddressRelativeMountPath}", "http://my-backend-api-s.default.svc.cluster.local:80");
            File.WriteAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepAdditionalConfigurationRelativeMountPath}", "{ \"DataSourceType\": \"Http\" }");
            File.WriteAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepAuthenticationMethodRelativeMountPath}", "UsernamePassword");
            File.WriteAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.EndpointProfileTypeRelativeMountPath}", "http-sql-dssroot");
            File.WriteAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepCertificateFileNameRelativeMountPath}", "some-certificate");
            File.WriteAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepUsernameFileNameRelativeMountPath}", "some-username");
            File.WriteAllText($"./AssetMonitorTestFiles/config/aep_config/{AssetMonitor.AepPasswordFileNameRelativeMountPath}", "some-password");
            File.WriteAllText($"./AssetMonitorTestFiles/secret/aep_username/some-username", "myusername");
            File.WriteAllText($"./AssetMonitorTestFiles/secret/aep_password/some-password", "mypassword");
            File.WriteAllText(
                "./AssetMonitorTestFiles/secret/aep_cert/some-certificate",
                "-----BEGIN CERTIFICATE-----\r\nMIICEjCCAXsCAg36MA0GCSqGSIb3DQEBBQUAMIGbMQswCQYDVQQGEwJKUDEOMAwG\r\nA1UECBMFVG9reW8xEDAOBgNVBAcTB0NodW8ta3UxETAPBgNVBAoTCEZyYW5rNERE\r\nMRgwFgYDVQQLEw9XZWJDZXJ0IFN1cHBvcnQxGDAWBgNVBAMTD0ZyYW5rNEREIFdl\r\nYiBDQTEjMCEGCSqGSIb3DQEJARYUc3VwcG9ydEBmcmFuazRkZC5jb20wHhcNMTIw\r\nODIyMDUyNjU0WhcNMTcwODIxMDUyNjU0WjBKMQswCQYDVQQGEwJKUDEOMAwGA1UE\r\nCAwFVG9reW8xETAPBgNVBAoMCEZyYW5rNEREMRgwFgYDVQQDDA93d3cuZXhhbXBs\r\nZS5jb20wXDANBgkqhkiG9w0BAQEFAANLADBIAkEAm/xmkHmEQrurE/0re/jeFRLl\r\n8ZPjBop7uLHhnia7lQG/5zDtZIUC3RVpqDSwBuw/NTweGyuP+o8AG98HxqxTBwID\r\nAQABMA0GCSqGSIb3DQEBBQUAA4GBABS2TLuBeTPmcaTaUW/LCB2NYOy8GMdzR1mx\r\n8iBIu2H6/E2tiY3RIevV2OW61qY2/XRQg7YPxx3ffeUugX9F4J/iPnnu1zAxxyBy\r\n2VguKv4SWjRFoRkIfIlHX0qVviMhSlNy2ioFLy7JcPZb+v3ftDGywUqcBiVDoea0\r\nHn+GmxZA\r\n-----END CERTIFICATE-----\r\n");
        }

        private void CleanupTestEnvironment()
        {
            bool cleanUpCompleted = false;
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(10));
            while (!cleanUpCompleted && !tokenSource.IsCancellationRequested)
            {
                try
                {
                    if (Directory.Exists("./AssetMonitorTestFiles/"))
                    {
                        Directory.Delete($"./AssetMonitorTestFiles/", true);
                    }

                    cleanUpCompleted = true;
                }
                catch
                {
                    // Some test files may still be opened in the test if it didn't complete as expected.
                }
            }
        }
    }
}
