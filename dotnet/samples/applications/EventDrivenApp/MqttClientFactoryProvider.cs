// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Mqtt.Session;
using Azure.Iot.Operations.Protocol.Connection;

namespace EventDrivenApp;

public class SessionClientFactory
{
    private readonly ILogger _logger;

    public SessionClientFactory(ILogger<SessionClientFactory> logger)
    {
        this._logger = logger;
    }

    public async Task<MqttSessionClient> GetSessionClient(string clientIdExtension)
    {
        MqttConnectionSettings settings = MqttConnectionSettings.FromEnvVars();
        settings.ClientId = "EventDrivenApp-" + clientIdExtension;

        _logger.LogInformation("Connecting to: {settings}", settings);

        MqttSessionClient sessionClient = new();
        await sessionClient.ConnectAsync(settings);

        return sessionClient;
    }
}
