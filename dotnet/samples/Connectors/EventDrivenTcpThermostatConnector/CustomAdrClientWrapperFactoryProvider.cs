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
    internal class CustomAdrClientWrapperFactoryProvider : IAdrClientWrapperFactoryProvider
    {
        public static Func<IServiceProvider, IAdrClientWrapperFactoryProvider> Factory = service =>
        {
            return new CustomAdrClientWrapperFactoryProvider();
        };

        public IAdrClientWrapper CreateAdrClientWrapper(ApplicationContext applicationContext, IMqttPubSubClient mqttPubSubClient)
        {
            // This demonstrates how you can configure the file watcher used in the AssetFileMonitor to use polling rather than fsnotify to check
            // for file changes
            return new AdrClientWrapper(new AdrServiceClient(applicationContext, mqttPubSubClient), new AssetFileMonitor(new PollingFilesMonitorFactory(TimeSpan.FromMilliseconds(500))));
        }
    }
}
