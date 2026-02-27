// <copyright file="WatermarkStore.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.StateStore;
using Microsoft.Extensions.Logging;

namespace Akri.HistorianConnector.Core.StateStore;

/// <summary>
/// Generic repository for storing watermark data in the AIO State Store.
/// </summary>
/// <typeparam name="T">The type of watermark data to store.</typeparam>
public sealed class WatermarkStore<T> : IWatermarkStore<T>
{
    private readonly ApplicationContext _applicationContext;
    private readonly IMqttClient _mqttClient;
    private readonly string _instanceId;
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WatermarkStore{T}"/> class.
    /// </summary>
    public WatermarkStore(
        ApplicationContext applicationContext,
        IMqttClient mqttClient,
        string instanceId,
        ILogger logger)
    {
        _applicationContext = applicationContext;
        _mqttClient = mqttClient;
        _instanceId = instanceId;
        _logger = logger;
    }

    /// <summary>
    /// Gets the data for a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The data, or default if not found.</returns>
    public async Task<T?> GetAsync(string key)
    {
        var fullKey = BuildKey(key);

        try
        {
            await using var client = new StateStoreClient(_applicationContext, _mqttClient);
            var response = await client.GetAsync(fullKey).ConfigureAwait(false);

            if (response.Value == null || response.Value.Bytes.Length == 0)
            {
                _logger.LogDebug("No data found for key {Key}", key);
                return default;
            }

            var data = JsonSerializer.Deserialize<T>(response.Value.Bytes, _jsonOptions);
            _logger.LogDebug("Retrieved data for key {Key}", key);
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve data for key {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Saves data for a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The data to save.</param>
    public async Task<bool> SetAsync(string key, T data)
    {
        var fullKey = BuildKey(key);
        var json = JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);

        try
        {
            await using var client = new StateStoreClient(_applicationContext, _mqttClient);
            var response = await client.SetAsync(key: fullKey, new StateStoreValue(json)).ConfigureAwait(false);

            if (response.Success)
            {
                _logger.LogDebug("Saved data for key {Key}", key);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to save data for key {Key}", key);
                return false;
            }
        }
        catch (AkriMqttException ex)
        {
            if (ex.Message.Contains("NoMatchingSubscribers"))
            {
                // For sample/demo environments without AIO state store, log warning but don't fail
                _logger.LogWarning("State store not available (NoMatchingSubscribers), skipping persistence for key {Key}", key);
                return true;
            }
            else
            {
                _logger.LogError(ex, "AkriMqttException with different message: {Message}", ex.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving data for key {Key}", key);
            throw;
        }
    }

    private string BuildKey(string key)
    {
        // Use a consistent key format with instance namespacing
        return $"{_instanceId}:{key}";
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
