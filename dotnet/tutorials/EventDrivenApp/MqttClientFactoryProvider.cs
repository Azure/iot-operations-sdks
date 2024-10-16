﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;
using k8s;

namespace EventDrivenApp;

public class SessionClientFactory
{
    private readonly ILogger logger;

    public SessionClientFactory(ILogger<SessionClientFactory> logger)
    {
        this.logger = logger;
    }

    public async Task<MqttSessionClient> GetSessionClient(string clientIdExtension)
    {
        MqttConnectionSettings settings;

        if (KubernetesClientConfiguration.IsInCluster())
        {
            // On cluster, read from the environment
            logger.LogInformation("Running in cluster, load config from environment");
            settings = MqttConnectionSettings.FromEnvVars();
            settings.ClientId += "-" + clientIdExtension;
        }
        else
        {
            // Local development, hard code the values
            logger.LogInformation("Running locally, setting config directly");
            settings = new("localhost")
            {
                ClientId = "EventDrivenApp-" + clientIdExtension,
                TcpPort = 8884,
                UseTls = true,
                CaFile = "../../../.session/broker-ca.crt",
                SatAuthFile = "../../../.session/token.txt",
                CleanStart = true
            };
        }

        logger.LogInformation("Connecting to: {settings}", settings);

        MqttSessionClient sessionClient = new();
        await sessionClient.ConnectAsync(settings);

        return sessionClient;
    }
}
