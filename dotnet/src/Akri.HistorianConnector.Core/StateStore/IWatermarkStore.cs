// <copyright file="IWatermarkStore.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

namespace Akri.HistorianConnector.Core.StateStore;

/// <summary>
/// Interface for storing and retrieving watermark data.
/// </summary>
/// <typeparam name="T">The type of watermark data to store.</typeparam>
public interface IWatermarkStore<T> : IAsyncDisposable
{
    /// <summary>
    /// Gets the data for a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The data, or default if not found.</returns>
    Task<T?> GetAsync(string key);

    /// <summary>
    /// Saves data for a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The data to save.</param>
    /// <returns>True if saved successfully.</returns>
    Task<bool> SetAsync(string key, T data);
}
