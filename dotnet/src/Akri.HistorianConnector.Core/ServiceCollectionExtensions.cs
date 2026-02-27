// <copyright file="ServiceCollectionExtensions.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using Akri.HistorianConnector.Core.Contracts;
using Akri.HistorianConnector.Core.StateStore;
using Microsoft.Extensions.Logging;
using Azure.Iot.Operations.Services.LeaderElection;
using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akri.HistorianConnector.Core;

/// <summary>
/// Extension methods for registering historian connector services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the core historian connector services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHistorianConnectorCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and validate options. Only call ValidateOnStart when a
        // HistorianConnector section is present in the configuration. When
        // running on AIO, ADR delivers configuration asynchronously and
        // ValidateOnStart would cause startup failures.
        var historianOptionsBuilder = services.AddOptions<HistorianConnectorOptions>()
            .Bind(configuration.GetSection(HistorianConnectorOptions.SectionName))
            .ValidateDataAnnotations();

        if (configuration.GetSection(HistorianConnectorOptions.SectionName).GetChildren().Any())
        {
            historianOptionsBuilder.ValidateOnStart();
        }

        // Register the raw options instance so it can be injected directly
        // (HistorianConnectorWorker takes HistorianConnectorOptions, not IOptions<T>)
        services.AddSingleton(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HistorianConnectorOptions>>().Value);

        // Register the default implementations
        services.AddSingleton<IAssetToQueryMapper, HistorianAssetToQueryMapper>();
        services.AddSingleton<IHistorianBatchSerializer, JsonHistorianBatchSerializer>();

        return services;
    }

    /// <summary>
    /// Adds the historian connector worker as a hosted service.
    /// </summary>
    /// <typeparam name="TQueryExecutor">The historian-specific query executor type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHistorianConnectorWorker<TQueryExecutor>(
        this IServiceCollection services)
        where TQueryExecutor : class, IHistorianQueryExecutor
    {
        // Register the historian-specific query executor
        services.AddSingleton<IHistorianQueryExecutor, TQueryExecutor>();

        // Register the connector worker as a hosted service
        services.AddHostedService<HistorianConnectorWorker>();

        return services;
    }

    /// <summary>
    /// Adds a custom asset-to-query mapper.
    /// </summary>
    /// <typeparam name="TMapper">The mapper type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAssetToQueryMapper<TMapper>(this IServiceCollection services)
        where TMapper : class, IAssetToQueryMapper
    {
        // Replace the default mapper
        services.AddSingleton<IAssetToQueryMapper, TMapper>();
        return services;
    }

    /// <summary>
    /// Adds a custom batch serializer.
    /// </summary>
    /// <typeparam name="TSerializer">The serializer type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHistorianBatchSerializer<TSerializer>(this IServiceCollection services)
        where TSerializer : class, IHistorianBatchSerializer
    {
        // Replace the default serializer
        services.AddSingleton<IHistorianBatchSerializer, TSerializer>();
        return services;
    }

    /// <summary>
    /// Adds all historian connector services including SDK, core, and worker.
    /// This is the recommended way to register a historian connector.
    /// </summary>
    /// <typeparam name="TQueryExecutor">The historian-specific query executor type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHistorianConnector<TQueryExecutor>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TQueryExecutor : class, IHistorianQueryExecutor
    {
        // Add AIO SDK infrastructure
        services.AddAioSdkServices();

        // Add core historian connector services
        services.AddHistorianConnectorCore(configuration);

        // Add the historian connector worker with the specified executor
        services.AddHistorianConnectorWorker<TQueryExecutor>();

        return services;
    }

    /// <summary>
    /// Adds the AIO SDK services required for the historian connector.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAioSdkServices(this IServiceCollection services)
    {
        // Register AIO SDK components
        services.AddSingleton<ApplicationContext>();
        services.AddSingleton(MqttSessionClientProvider.Factory);
        services.AddSingleton<IAzureDeviceRegistryClientWrapperProvider>(
            AzureDeviceRegistryClientWrapperProvider.Factory);
        services.AddSingleton<IMessageSchemaProvider, DefaultMessageSchemaProvider>();

        return services;
    }

    /// <summary>
    /// Registers a watermark store implementation for the connector.
    /// The caller supplies a factory that derives the instance id used for namespacing.
    /// </summary>
    public static IServiceCollection AddConnectorWatermarkStore<T>(
        this IServiceCollection services,
        Func<IServiceProvider, string> instanceIdFactory)
    {
        services.AddSingleton<IWatermarkStore<T>>(sp =>
        {
            var appContext = sp.GetRequiredService<ApplicationContext>();
            var mqttClient = sp.GetRequiredService<IMqttClient>();
            var instanceId = instanceIdFactory(sp);
            var logger = sp.GetRequiredService<ILogger<WatermarkStore<T>>>();
            return new WatermarkStore<T>(appContext, mqttClient, instanceId, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an optional leader election client for the connector.
    /// The enabled predicate is consulted at runtime to determine whether to create the client.
    /// </summary>
    public static IServiceCollection AddConnectorLeaderElection(
        this IServiceCollection services,
        Func<IServiceProvider, bool> enabledPredicate,
        Func<IServiceProvider, string> leadershipIdFactory)
    {
        services.AddSingleton(sp =>
        {
            if (!enabledPredicate(sp))
            {
                return (LeaderElectionClient?)null;
            }

            var appContext = sp.GetRequiredService<ApplicationContext>();
            var mqttClient = sp.GetRequiredService<IMqttClient>();
            var leadershipPositionId = leadershipIdFactory(sp);
            var client = new LeaderElectionClient(appContext, mqttClient, leadershipPositionId);

            client.AutomaticRenewalOptions = new LeaderElectionAutomaticRenewalOptions
            {
                AutomaticRenewal = true,
                ElectionTerm = TimeSpan.FromSeconds(15),
                RenewalPeriod = TimeSpan.FromSeconds(10)
            };

            return client;
        });

        return services;
    }
}

/// <summary>
/// Default implementation of IMessageSchemaProvider for use with historian connectors.
/// This provides a minimal schema provider compatible with Azure IoT Operations.
/// </summary>
public sealed class DefaultMessageSchemaProvider : IMessageSchemaProvider
{
    /// <summary>
    /// Gets the message schema for an asset event.
    /// </summary>
    public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(
        Device device,
        Asset asset,
        string schemaName,
        AssetEvent eventData,
        CancellationToken cancellationToken)
    {
        // Return null to indicate no custom schema is defined
        // The connector will use default message handling
        return Task.FromResult((ConnectorMessageSchema?)null);
    }

    /// <summary>
    /// Gets the message schema for an asset dataset.
    /// </summary>
    public Task<ConnectorMessageSchema?> GetMessageSchemaAsync(
        Device device,
        Asset asset,
        string schemaName,
        AssetDataset datasetData,
        CancellationToken cancellationToken)
    {
        // Return null to indicate no custom schema is defined
        // The connector will use default message handling
        return Task.FromResult((ConnectorMessageSchema?)null);
    }
}
