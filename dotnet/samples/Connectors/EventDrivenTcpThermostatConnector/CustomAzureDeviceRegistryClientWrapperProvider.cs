// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.Files;
using Azure.Iot.Operations.Connector.Files.FilesMonitor;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

namespace EventDrivenTcpThermostatConnector
{
    internal class CustomAzureDeviceRegistryClientWrapperProvider : IAzureDeviceRegistryClientWrapperProvider
    {
        public static Func<IServiceProvider, IAzureDeviceRegistryClientWrapperProvider> Factory = service =>
        {
            return new CustomAzureDeviceRegistryClientWrapperProvider();
        };

        public IAzureDeviceRegistryClientWrapper CreateAdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient)
        {
            // This demonstrates how you can configure the file watcher used in the AssetFileMonitor to use polling rather than fsnotify to check
            // for file changes
            return new AzureDeviceRegistryClientWrapper(new AzureDeviceRegistryClient(applicationContext, mqttPubSubClient), new AssetFileMonitor(new PollingFilesMonitorFactory(TimeSpan.FromMilliseconds(500))));
        }
    }
}
