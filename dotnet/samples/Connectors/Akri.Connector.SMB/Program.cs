// <copyright file="Program.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.Connector.SMB;
using Akri.Connector.SMB.Models;
using Akri.HistorianConnector.Core;
using Akri.HistorianConnector.Core.Contracts;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// Host.CreateDefaultBuilder automatically loads:
// - appsettings.json (local dev)
// - appsettings.{Environment}.json
// - Environment variables
// - Command line args
// In production on AIO, config comes from ADR at runtime.
var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.TimestampFormat = "HH:mm:ss ";
        });
    })
    .ConfigureServices((context, services) =>
    {
        // Register AIO SDK services (following SDK sample pattern)
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientProvider.Factory);
        services.AddSingleton<IAzureDeviceRegistryClientWrapperProvider>(
            AzureDeviceRegistryClientWrapperProvider.Factory);
        services.AddSingleton<IMessageSchemaProvider, DefaultMessageSchemaProvider>();

        // Bind configuration (no ValidateOnStart - ADR provides config at runtime)
        services.Configure<HistorianConnectorOptions>(
            context.Configuration.GetSection("HistorianConnector"));
        services.Configure<SMBConnectorOptions>(
            context.Configuration.GetSection("SMBConnector"));

        // Register HistorianConnectorOptions as singleton for direct injection
        services.AddSingleton(sp =>
            sp.GetRequiredService<IOptions<HistorianConnectorOptions>>().Value);

        // Register historian connector core services
        services.AddSingleton<IAssetToQueryMapper, HistorianAssetToQueryMapper>();
        services.AddSingleton<IHistorianBatchSerializer, JsonHistorianBatchSerializer>();

        // Register SMB-specific services
        services.AddSingleton<IHistorianQueryExecutor, SMBHistorianExecutor>();
        services.AddSingleton<ISMBClient, SMBClient>();

        // Helper to resolve SMB options from the service provider. Use this
        // delegate in multiple factory registrations to avoid repeated calls.
        static SMBConnectorOptions getSmbOptions(IServiceProvider sp) =>
            sp.GetRequiredService<IOptions<SMBConnectorOptions>>().Value;

        // Watermark store - use shared helper and derive instance id from options
        services.AddConnectorWatermarkStore<WatermarkData>(sp =>
            $"smb-connector:{getSmbOptions(sp).InstanceId}");

        // Leader election (optional) - use shared helper to create client when enabled
        services.AddConnectorLeaderElection(
            sp => getSmbOptions(sp).EnableLeaderElection,
            sp => $"smb-connector-{Environment.MachineName}");

        // Register the connector worker as hosted service
        services.AddHostedService<HistorianConnectorWorker>();
    })
    .Build();

await host.RunAsync();
