// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Xunit;

namespace Azure.Iot.Operations.Services.UnitTests.AssetAndDeviceRegistry
{
    public class AssetStatusTests
    {
        [Fact]
        public void TestComparison()
        {
            AssetStatus status1 = createTestStatus();
            AssetStatus status2 = createTestStatus();

            Assert.True(status1.EqualTo(status2));

            status1.Config!.LastTransitionTime = DateTime.Now;

            // This function should ignore any differences in date times
            Assert.True(status2.EqualTo(status1));

            status1.Config.Error = new ConfigError()
            {
                Code = "someErrorCode"
            };

            Assert.False(status1.EqualTo(status2));

            AssetStatus status3 = createTestStatus();

            status3.Streams ??= new();
            status3.Streams.Add(new AssetDatasetEventStreamStatus()
            {
                Name = "someAssetStreamName"
            });

            Assert.False(status2.EqualTo(status3));

            AssetStatus status4 = createTestStatus();
            status4.Datasets = new();
            Assert.False(status2.EqualTo(status4));
        }

        private AssetStatus createTestStatus()
        {
            return new AssetStatus()
            {
                Config = new ConfigStatus()
                {
                    Version = 1,
                    LastTransitionTime = DateTime.MinValue
                },
                Datasets = new List<AssetDatasetEventStreamStatus>
                {
                    new AssetDatasetEventStreamStatus()
                    {
                        Name = "someDatasetName",
                    }
                },
                EventGroups = new List<AssetEventGroupStatus>()
                {
                    new AssetEventGroupStatus()
                    {
                        Name = "someEventGroupName",
                        Events = new List<AssetDatasetEventStreamStatus>()
                        {
                            new AssetDatasetEventStreamStatus()
                            {
                                Name = "someEvent"
                            }
                        }
                    }
                }
            };
        }
    }
}
