// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Azure.Iot.Operations.Services.Assets;
using RestThermostatConnector;

namespace EventDrivenRestThermostatConnector
{
    internal class MockDataSource
    {
        public event EventHandler<MockDataReceivedEventArgs>? OnDataReceived;
        private CancellationTokenSource? cancellationTokenSource;
        private string _assetName;
        private string _datasetName;

        public MockDataSource(string assetName, string datasetName) 
        { 
            _assetName = assetName;
            _datasetName = datasetName;
        }

        public void Open()
        {
            cancellationTokenSource = new();
            new Task(async () =>
            {
                try
                {
                    while (!cancellationTokenSource.IsCancellationRequested)
                    {
                        Random random = new Random();
                        await Task.Delay(random.Next(1000, 5000), cancellationTokenSource.Token);
                        ThermostatStatus status = new()
                        {
                            CurrentTemperature = random.Next(50, 70),
                            DesiredTemperature = random.Next(50, 70)
                        };

                        var eventArgs = new MockDataReceivedEventArgs(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(status)), _assetName, _datasetName);
                        OnDataReceived?.Invoke(this, eventArgs);
                    }
                }
                catch (OperationCanceledException e)
                {
                    // Thread was stopped, end gracefully
                }
            }).Start();
        }

        public void Close()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
    }
}
