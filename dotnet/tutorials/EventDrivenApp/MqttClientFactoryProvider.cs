// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using k8s;

namespace EventDrivenApp;

internal static class MqttClientFactoryProvider
{
    public static Func<IServiceProvider, MqttSessionClient> MqttSessionClientFactory = service =>
    {
        return new MqttSessionClient();
    };

    public static Func<IServiceProvider, MqttConnectionSettings> MqttConnectionSettingsFactory = service =>
    {
        ILogger? logger = service.GetService<ILogger<MqttSessionClient>>();
        IConfiguration? configuration = service.GetService<IConfiguration>();

        MqttConnectionSettings settings;

        if (KubernetesClientConfiguration.IsInCluster())
        {
            logger!.LogInformation("Running in cluster, load config from environment");
            settings = MqttConnectionSettings.FromEnvVars();
        }
        else
        {
            logger!.LogInformation("Running locally, load config from connection string");
            settings = MqttConnectionSettings.FromConnectionString(configuration!.GetConnectionString("Default")!);
        }

        return settings;
    };
}
