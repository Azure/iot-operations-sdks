// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;
using static Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry.MockAzureDeviceRegistryClient;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry
{
    public class AssetHealthStatusReporterTests
    {
        // Test ideas:
        // test that pausing one dataset doesn't affect reporting of another dataset

        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task TestReportingDatasetsFirstHealth()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string datasetName = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetHealthStatusReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            await reporter.ReportDatasetHealthStatusAsync(datasetName, initialReportedHealth);

            // Assert that the first health status is passed through the ADR client (it wasn't skipped due to any caching logic)
            Assert.Equal(deviceName, mockAdrClient.ReportedDatasetRuntimeHealths.First().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDatasetRuntimeHealths.First().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedDatasetRuntimeHealths.First().AssetName);
            Assert.Equal(datasetName, mockAdrClient.ReportedDatasetRuntimeHealths.First().DatasetName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedDatasetRuntimeHealths.First().RuntimeHealth));

            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedDatasetRuntimeHealths.Count <= 1)
                {
                    await Task.Delay(reportingPeriod);
                }
            });

            // Wait for background reporting to start for the above device endpoint (or time out)
            try
            {
                await checkForBackgroundReports.WaitAsync(_timeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for background reporting of the device endpoint status to start");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedDatasetRuntimeHealths.Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDatasetRuntimeHealths.Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedDatasetRuntimeHealths.Last().AssetName);
            Assert.Equal(datasetName, mockAdrClient.ReportedDatasetRuntimeHealths.Last().DatasetName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedDatasetRuntimeHealths.Last().RuntimeHealth));

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            reporter.PauseReportingDataset(datasetName);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedDatasetRuntimeHealths.Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedDatasetRuntimeHealths.Count;

            // Report some new health status to check that periodic reporting resumes
            var secondReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 2,
                Message = "some new message",
                Status = HealthStatus.Unavailable,
            };

            await reporter.ReportDatasetHealthStatusAsync(datasetName, secondReportedHealth);

            checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedDatasetRuntimeHealths.Count <= lastReportCountAfterPause + 1)
                {
                    await Task.Delay(reportingPeriod);
                }
            });

            // Wait for background reporting to resume for the above device endpoint (or time out)
            try
            {
                await checkForBackgroundReports.WaitAsync(_timeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for background reporting of the dataset to resume");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedDatasetRuntimeHealths.Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDatasetRuntimeHealths.Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedDatasetRuntimeHealths.Last().AssetName);
            Assert.Equal(datasetName, mockAdrClient.ReportedDatasetRuntimeHealths.Last().DatasetName);
            Assert.True(RuntimeHealth.Equals(secondReportedHealth, mockAdrClient.ReportedDatasetRuntimeHealths.Last().RuntimeHealth));
        }
    }
}
