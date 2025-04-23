// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Connector.ConnectorConfigurations;
using Azure.Iot.Operations.Protocol;
using RestThermostatConnector;

Trace.Listeners.Add(new ConsoleTraceListener()); //TODO revert

string connectorClientId = Environment.GetEnvironmentVariable(ConnectorFileMountSettings.ConnectorClientIdEnvVar) ?? "todo";

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientFactoryProvider.MqttSessionClientFactory);
        services.AddSingleton(RestThermostatDatasetSamplerFactory.RestDatasetSourceFactoryProvider);
        services.AddSingleton(NoMessageSchemaProvider.NoMessageSchemaProviderFactory);
        services.AddSingleton(LeaderElectionConfigurationProvider.ConnectorLeaderElectionConfigurationProviderFactory);
        services.AddSingleton<IAdrClientWrapper>((services) => new AdrClientWrapper(services.GetService<ApplicationContext>()!, services.GetService<IMqttClient>()!, connectorClientId));
        services.AddHostedService<PollingTelemetryConnectorWorker>();
    })
    .Build();

host.Run();
