// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Moq;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry
{
    public class DeviceEndpointHealthStatusReporterTests
    {
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task TestReportingDeviceEndpointsFirstHealth()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            DeviceEndpointHealthStatusReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            await reporter.ReportDeviceEndpointRuntimeHealthAsync(initialReportedHealth);

            // Assert that the first health status is passed through the ADR client (it wasn't skipped due to any caching logic)
            Assert.Equal(deviceName, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.First().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.First().InboundEndpointName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.First().RuntimeHealth));

            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Count <= 1)
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
            Assert.Equal(deviceName, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Last().InboundEndpointName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Last().RuntimeHealth));

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.CancelHealthStatusReportingAsync();
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Count;

            // Report some new health status to check that periodic reporting resumes
            var secondReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 2,
                Message = "some new message",
                Status = HealthStatus.Unavailable,
            };

            await reporter.ReportDeviceEndpointRuntimeHealthAsync(secondReportedHealth);

            checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Count <= lastReportCountAfterPause + 1)
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
                Assert.Fail("Timed out waiting for background reporting of the device endpoint status to resume");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Last().InboundEndpointName);
            Assert.True(RuntimeHealth.Equals(secondReportedHealth, mockAdrClient.ReportedDeviceEndpointRuntimeHealths.Last().RuntimeHealth));
        }
    }
}
