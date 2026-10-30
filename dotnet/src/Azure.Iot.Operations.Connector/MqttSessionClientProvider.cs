// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Azure.Iot.Operations.Connector
{
    public static class MqttSessionClientProvider
    {
        /// <summary>
        /// Categorizes the internal traffic between the connector and other AIO services.
        /// </summary>
        private const string ConnectorMetricCategory = "aiosdk-dotnet-connector";

        public static Func<IServiceProvider, IMqttClient> Factory = service =>
        {
            IConfiguration? config = service.GetService<IConfiguration>();
            bool mqttDiag = config!.GetValue<bool>("mqttDiag");
            if (mqttDiag)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
            }

            MqttSessionClientOptions sessionClientOptions = new()
            {
                EnableMqttLogging = mqttDiag,
                RetryOnFirstConnect = true,
                MetricCategory = ConnectorMetricCategory,
            };


            return new MqttSessionClient(sessionClientOptions);
        };
    }
}