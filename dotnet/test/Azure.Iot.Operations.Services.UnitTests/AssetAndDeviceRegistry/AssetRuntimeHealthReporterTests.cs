// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry
{
    public class AssetRuntimeHealthReporterTests
    {
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
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            await reporter.ReportDatasetHealthStatusAsync(datasetName, initialReportedHealth);

            // Assert that the first health status is passed through the ADR client (it wasn't skipped due to any caching logic)
            Assert.True(mockAdrClient.ReportedDatasetRuntimeHealths.ContainsKey(datasetName));
            Assert.Equal(deviceName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].First().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].First().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].First().AssetName);
            Assert.Equal(datasetName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].First().DatasetName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].First().RuntimeHealth));

            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Count <= 1)
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
                Assert.Fail("Timed out waiting for background reporting of the dataset status to start");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.True(mockAdrClient.ReportedDatasetRuntimeHealths.ContainsKey(datasetName));
            Assert.Equal(deviceName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().AssetName);
            Assert.Equal(datasetName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().DatasetName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().RuntimeHealth));

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingDatasetAsync(datasetName);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().DateTimeReported) > (reportingPeriod * 2));
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
                while (mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Count <= lastReportCountAfterPause + 3)
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
            Assert.Equal(deviceName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().AssetName);
            Assert.Equal(datasetName, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().DatasetName);
            Assert.True(RuntimeHealth.Equals(secondReportedHealth, mockAdrClient.ReportedDatasetRuntimeHealths[datasetName].Last().RuntimeHealth));
        }

        [Fact]
        public async Task PausingOneDatasetsReportingDoesNotPauseOthers()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string dataset1Name = Guid.NewGuid().ToString();
            string dataset2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both datasets
            await reporter.ReportDatasetHealthStatusAsync(dataset1Name, initialReportedHealth);
            await reporter.ReportDatasetHealthStatusAsync(dataset2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingDatasetAsync(dataset1Name);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedDatasetRuntimeHealths[dataset1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedDatasetRuntimeHealths[dataset2Name].Count;

            // Wait a bit to confirm that dataset 2 is still sending periodic health updates
            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedDatasetRuntimeHealths[dataset2Name].Count <= lastReportCountAfterPause + 3)
                {
                    await Task.Delay(reportingPeriod);
                }
            });

            try
            {
                await checkForBackgroundReports.WaitAsync(_timeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for background reporting of the dataset to continue");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedDatasetRuntimeHealths[dataset2Name].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedDatasetRuntimeHealths[dataset2Name].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedDatasetRuntimeHealths[dataset2Name].Last().AssetName);
            Assert.Equal(dataset2Name, mockAdrClient.ReportedDatasetRuntimeHealths[dataset2Name].Last().DatasetName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedDatasetRuntimeHealths[dataset2Name].Last().RuntimeHealth));
        }

        [Fact]
        public async Task CancellingAllReportingStopsMultipleDatasetsFromReporting()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string dataset1Name = Guid.NewGuid().ToString();
            string dataset2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both datasets
            await reporter.ReportDatasetHealthStatusAsync(dataset1Name, initialReportedHealth);
            await reporter.ReportDatasetHealthStatusAsync(dataset2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.CancelHealthStatusReportingAsync();
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report for each dataset happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedDatasetRuntimeHealths[dataset1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedDatasetRuntimeHealths[dataset2Name].Last().DateTimeReported) > (reportingPeriod * 2));
        }

        [Fact]
        public async Task TestReportingStreamsFirstHealth()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string streamName = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            await reporter.ReportStreamHealthStatusAsync(streamName, initialReportedHealth);

            // Assert that the first health status is passed through the ADR client (it wasn't skipped due to any caching logic)
            Assert.True(mockAdrClient.ReportedStreamRuntimeHealths.ContainsKey(streamName));
            Assert.Equal(deviceName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].First().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].First().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].First().AssetName);
            Assert.Equal(streamName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].First().StreamName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedStreamRuntimeHealths[streamName].First().RuntimeHealth));

            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedStreamRuntimeHealths[streamName].Count <= 1)
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
                Assert.Fail("Timed out waiting for background reporting of the stream status to start");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.True(mockAdrClient.ReportedStreamRuntimeHealths.ContainsKey(streamName));
            Assert.Equal(deviceName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().AssetName);
            Assert.Equal(streamName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().StreamName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().RuntimeHealth));

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingStreamAsync(streamName);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedStreamRuntimeHealths.Count;

            // Report some new health status to check that periodic reporting resumes
            var secondReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 2,
                Message = "some new message",
                Status = HealthStatus.Unavailable,
            };

            await reporter.ReportStreamHealthStatusAsync(streamName, secondReportedHealth);

            checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedStreamRuntimeHealths[streamName].Count <= lastReportCountAfterPause + 3)
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
                Assert.Fail("Timed out waiting for background reporting of the stream to resume");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().AssetName);
            Assert.Equal(streamName, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().StreamName);
            Assert.True(RuntimeHealth.Equals(secondReportedHealth, mockAdrClient.ReportedStreamRuntimeHealths[streamName].Last().RuntimeHealth));
        }

        [Fact]
        public async Task PausingOneStreamsReportingDoesNotPauseOthers()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string stream1Name = Guid.NewGuid().ToString();
            string stream2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both streams
            await reporter.ReportStreamHealthStatusAsync(stream1Name, initialReportedHealth);
            await reporter.ReportStreamHealthStatusAsync(stream2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingStreamAsync(stream1Name);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedStreamRuntimeHealths[stream1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedStreamRuntimeHealths[stream2Name].Count;

            // Wait a bit to confirm that stream 2 is still sending periodic health updates
            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedStreamRuntimeHealths[stream2Name].Count <= lastReportCountAfterPause + 3)
                {
                    await Task.Delay(reportingPeriod);
                }
            });

            try
            {
                await checkForBackgroundReports.WaitAsync(_timeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for background reporting of the stream to continue");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedStreamRuntimeHealths[stream2Name].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedStreamRuntimeHealths[stream2Name].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedStreamRuntimeHealths[stream2Name].Last().AssetName);
            Assert.Equal(stream2Name, mockAdrClient.ReportedStreamRuntimeHealths[stream2Name].Last().StreamName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedStreamRuntimeHealths[stream2Name].Last().RuntimeHealth));
        }

        [Fact]
        public async Task CancellingAllReportingStopsMultipleStreamsFromReporting()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string stream1Name = Guid.NewGuid().ToString();
            string stream2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both streams
            await reporter.ReportStreamHealthStatusAsync(stream1Name, initialReportedHealth);
            await reporter.ReportStreamHealthStatusAsync(stream2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.CancelHealthStatusReportingAsync();
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report for each stream happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedStreamRuntimeHealths[stream1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedStreamRuntimeHealths[stream2Name].Last().DateTimeReported) > (reportingPeriod * 2));
        }

        [Fact]
        public async Task TestReportingEventsFirstHealth()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string eventGroupName = Guid.NewGuid().ToString();
            string eventName = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            await reporter.ReportEventHealthStatusAsync(eventGroupName, eventName, initialReportedHealth);

            // Assert that the first health status is passed through the ADR client (it wasn't skipped due to any caching logic)
            Assert.True(mockAdrClient.ReportedEventRuntimeHealths.ContainsKey(eventGroupName));
            Assert.True(mockAdrClient.ReportedEventRuntimeHealths[eventGroupName].ContainsKey(eventName));
            Assert.Equal(deviceName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].First().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].First().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].First().AssetName);
            Assert.Equal(eventGroupName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].First().EventGroupName);
            Assert.Equal(eventName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].First().EventName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].First().RuntimeHealth));

            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Count <= 1)
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
                Assert.Fail("Timed out waiting for background reporting of the event status to start");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.True(mockAdrClient.ReportedEventRuntimeHealths.ContainsKey(eventGroupName));
            Assert.True(mockAdrClient.ReportedEventRuntimeHealths[eventGroupName].ContainsKey(eventName));
            Assert.Equal(deviceName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().AssetName);
            Assert.Equal(eventGroupName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().EventGroupName);
            Assert.Equal(eventName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().EventName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().RuntimeHealth));

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingEventAsync(eventGroupName, eventName);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedEventRuntimeHealths.Count;

            // Report some new health status to check that periodic reporting resumes
            var secondReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 2,
                Message = "some new message",
                Status = HealthStatus.Unavailable,
            };

            await reporter.ReportEventHealthStatusAsync(eventGroupName, eventName, secondReportedHealth);

            checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Count <= lastReportCountAfterPause + 3)
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
                Assert.Fail("Timed out waiting for background reporting of the stream to resume");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().AssetName);
            Assert.Equal(eventGroupName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().EventGroupName);
            Assert.Equal(eventName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().EventName);
            Assert.True(RuntimeHealth.Equals(secondReportedHealth, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][eventName].Last().RuntimeHealth));
        }

        [Fact]
        public async Task PausingOneEventsInSeparateEventGroupsReportingDoesNotPauseOthers()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string eventGroup1Name = Guid.NewGuid().ToString();
            string event1Name = Guid.NewGuid().ToString();
            string eventGroup2Name = Guid.NewGuid().ToString();
            string event2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both events
            await reporter.ReportEventHealthStatusAsync(eventGroup1Name, event1Name, initialReportedHealth);
            await reporter.ReportEventHealthStatusAsync(eventGroup2Name, event2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingEventAsync(eventGroup1Name, event1Name);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedEventRuntimeHealths[eventGroup1Name][event1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Count;

            // Wait a bit to confirm that event 2 is still sending periodic health updates
            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Count <= lastReportCountAfterPause + 3)
                {
                    await Task.Delay(reportingPeriod);
                }
            });

            try
            {
                await checkForBackgroundReports.WaitAsync(_timeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for background reporting of the event to continue");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Last().AssetName);
            Assert.Equal(eventGroup2Name, mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Last().EventGroupName);
            Assert.Equal(event2Name, mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Last().EventName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Last().RuntimeHealth));
        }

        [Fact]
        public async Task PausingOneEventsReportingInSameEventGroupDoesNotPauseOthers()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string eventGroupName = Guid.NewGuid().ToString();
            string event1Name = Guid.NewGuid().ToString();
            string event2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both events
            await reporter.ReportEventHealthStatusAsync(eventGroupName, event1Name, initialReportedHealth);
            await reporter.ReportEventHealthStatusAsync(eventGroupName, event2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingEventAsync(eventGroupName, event1Name);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event2Name].Count;

            // Wait a bit to confirm that event 2 is still sending periodic health updates
            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event2Name].Count <= lastReportCountAfterPause + 3)
                {
                    await Task.Delay(reportingPeriod);
                }
            });

            try
            {
                await checkForBackgroundReports.WaitAsync(_timeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for background reporting of the event to continue");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event2Name].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event2Name].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event2Name].Last().AssetName);
            Assert.Equal(eventGroupName, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event2Name].Last().EventGroupName);
            Assert.Equal(event2Name, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event2Name].Last().EventName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedEventRuntimeHealths[eventGroupName][event2Name].Last().RuntimeHealth));
        }

        [Fact]
        public async Task CancellingAllReportingStopsMultipleEventsFromReporting()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string eventGroup1Name = Guid.NewGuid().ToString();
            string event1Name = Guid.NewGuid().ToString();
            string eventGroup2Name = Guid.NewGuid().ToString();
            string event2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both events
            await reporter.ReportEventHealthStatusAsync(eventGroup1Name, event1Name, initialReportedHealth);
            await reporter.ReportEventHealthStatusAsync(eventGroup2Name, event2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.CancelHealthStatusReportingAsync();
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report for each event happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedEventRuntimeHealths[eventGroup1Name][event1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedEventRuntimeHealths[eventGroup2Name][event2Name].Last().DateTimeReported) > (reportingPeriod * 2));
        }

        [Fact]
        public async Task TestReportingManagementActionFirstHealth()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string managementGroupName = Guid.NewGuid().ToString();
            string managementActionName = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            await reporter.ReportManagementActionHealthStatusAsync(managementGroupName, managementActionName, initialReportedHealth);

            // Assert that the first health status is passed through the ADR client (it wasn't skipped due to any caching logic)
            Assert.True(mockAdrClient.ReportedManagementActionRuntimeHealths.ContainsKey(managementGroupName));
            Assert.True(mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName].ContainsKey(managementActionName));
            Assert.Equal(deviceName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].First().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].First().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].First().AssetName);
            Assert.Equal(managementGroupName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].First().ManagementGroupName);
            Assert.Equal(managementActionName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].First().ManagementActionName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].First().RuntimeHealth));

            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Count <= 1)
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
                Assert.Fail("Timed out waiting for background reporting of the managementAction status to start");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.True(mockAdrClient.ReportedManagementActionRuntimeHealths.ContainsKey(managementGroupName));
            Assert.True(mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName].ContainsKey(managementActionName));
            Assert.Equal(deviceName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().AssetName);
            Assert.Equal(managementGroupName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().ManagementGroupName);
            Assert.Equal(managementActionName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().ManagementActionName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().RuntimeHealth));

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingManagementActionAsync(managementGroupName, managementActionName);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedManagementActionRuntimeHealths.Count;

            // Report some new health status to check that periodic reporting resumes
            var secondReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 2,
                Message = "some new message",
                Status = HealthStatus.Unavailable,
            };

            await reporter.ReportManagementActionHealthStatusAsync(managementGroupName, managementActionName, secondReportedHealth);

            checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Count <= lastReportCountAfterPause + 3)
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
                Assert.Fail("Timed out waiting for background reporting of the stream to resume");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().AssetName);
            Assert.Equal(managementGroupName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().ManagementGroupName);
            Assert.Equal(managementActionName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().ManagementActionName);
            Assert.True(RuntimeHealth.Equals(secondReportedHealth, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementActionName].Last().RuntimeHealth));
        }

        [Fact]
        public async Task PausingOneManagementActionInSeparateManagementGroupsReportingDoesNotPauseOthers()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string managementGroup1Name = Guid.NewGuid().ToString();
            string managementAction1Name = Guid.NewGuid().ToString();
            string managementGroup2Name = Guid.NewGuid().ToString();
            string managementAction2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both managementActions
            await reporter.ReportManagementActionHealthStatusAsync(managementGroup1Name, managementAction1Name, initialReportedHealth);
            await reporter.ReportManagementActionHealthStatusAsync(managementGroup2Name, managementAction2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingManagementActionAsync(managementGroup1Name, managementAction1Name);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup1Name][managementAction1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Count;

            // Wait a bit to confirm that managementAction 2 is still sending periodic health updates
            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Count <= lastReportCountAfterPause + 3)
                {
                    await Task.Delay(reportingPeriod);
                }
            });

            try
            {
                await checkForBackgroundReports.WaitAsync(_timeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for background reporting of the management action to continue");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Last().AssetName);
            Assert.Equal(managementGroup2Name, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Last().ManagementGroupName);
            Assert.Equal(managementAction2Name, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Last().ManagementActionName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Last().RuntimeHealth));
        }

        [Fact]
        public async Task PausingOneManagementActionReportingInSameManagementGroupDoesNotPauseOthers()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string managementGroupName = Guid.NewGuid().ToString();
            string managementAction1Name = Guid.NewGuid().ToString();
            string managementAction2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both managementActions
            await reporter.ReportManagementActionHealthStatusAsync(managementGroupName, managementAction1Name, initialReportedHealth);
            await reporter.ReportManagementActionHealthStatusAsync(managementGroupName, managementAction2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.PauseReportingManagementActionAsync(managementGroupName, managementAction1Name);
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            int lastReportCountAfterPause = mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction2Name].Count;

            // Wait a bit to confirm that managementAction 2 is still sending periodic health updates
            Task checkForBackgroundReports = Task.Run(async () =>
            {
                while (mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction2Name].Count <= lastReportCountAfterPause + 3)
                {
                    await Task.Delay(reportingPeriod);
                }
            });

            try
            {
                await checkForBackgroundReports.WaitAsync(_timeout);
            }
            catch (TimeoutException)
            {
                Assert.Fail("Timed out waiting for background reporting of the management action to continue");
            }

            // Assert that the latest health status (which should be sent periodically) matches the first health status
            Assert.Equal(deviceName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction2Name].Last().DeviceName);
            Assert.Equal(inboundEndpointName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction2Name].Last().InboundEndpointName);
            Assert.Equal(assetName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction2Name].Last().AssetName);
            Assert.Equal(managementGroupName, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction2Name].Last().ManagementGroupName);
            Assert.Equal(managementAction2Name, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction2Name].Last().ManagementActionName);
            Assert.True(RuntimeHealth.Equals(initialReportedHealth, mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroupName][managementAction2Name].Last().RuntimeHealth));
        }

        [Fact]
        public async Task CancellingAllReportingStopsMultipleManagementActionFromReporting()
        {
            string deviceName = Guid.NewGuid().ToString();
            string inboundEndpointName = Guid.NewGuid().ToString();
            string assetName = Guid.NewGuid().ToString();
            string managementGroup1Name = Guid.NewGuid().ToString();
            string managementAction1Name = Guid.NewGuid().ToString();
            string managementGroup2Name = Guid.NewGuid().ToString();
            string managementAction2Name = Guid.NewGuid().ToString();
            TimeSpan reportingPeriod = TimeSpan.FromMilliseconds(10);

            MockAzureDeviceRegistryClient mockAdrClient = new MockAzureDeviceRegistryClient();
            AssetRuntimeHealthReporter reporter = new(mockAdrClient, deviceName, inboundEndpointName, assetName, reportingPeriod);

            var initialReportedHealth = new RuntimeHealth()
            {
                LastUpdateTime = DateTime.UtcNow,
                Version = 1,
                Message = "some message",
                Status = HealthStatus.Available,
            };

            // Start the periodic sending of runtime healths for both managementActions
            await reporter.ReportManagementActionHealthStatusAsync(managementGroup1Name, managementAction1Name, initialReportedHealth);
            await reporter.ReportManagementActionHealthStatusAsync(managementGroup2Name, managementAction2Name, initialReportedHealth);

            // Cancel background reporting and then wait a bit to ensure that background reporting has stopped
            await reporter.CancelHealthStatusReportingAsync();
            await Task.Delay(reportingPeriod * 5);

            // Check that the last periodic report for each managementAction happened at least 2 * reportingPeriod ago (signaling that the background reporting has probably stopped)
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup1Name][managementAction1Name].Last().DateTimeReported) > (reportingPeriod * 2));
            Assert.True(DateTime.UtcNow.Subtract(mockAdrClient.ReportedManagementActionRuntimeHealths[managementGroup2Name][managementAction2Name].Last().DateTimeReported) > (reportingPeriod * 2));
        }
    }
}
