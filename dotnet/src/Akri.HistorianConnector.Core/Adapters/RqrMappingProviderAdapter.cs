// <copyright file="RqrMappingProviderAdapter.cs">
// Copyright (c) Mesh Systems. Licensed under the MIT License.
// </copyright>

using System.Collections.Concurrent;
using Akri.HistorianConnector.Core.Models;
using ResilientQueryRunner;

namespace Akri.HistorianConnector.Core.Adapters;

/// <summary>
/// Adapts the active query definitions to RQR's IMappingConfigurationProvider.
/// </summary>
internal sealed class RqrMappingProviderAdapter : IMappingConfigurationProvider
{
    private readonly ConcurrentDictionary<string, HistorianQueryDefinition> _queryDefinitions;

    public RqrMappingProviderAdapter(ConcurrentDictionary<string, HistorianQueryDefinition> queryDefinitions)
    {
        _queryDefinitions = queryDefinitions;
    }

    public MappingConfiguration GetMapping(QueryId queryId)
    {
        var query = _queryDefinitions.TryGetValue(queryId.Value, out var q) ? q : null;
        return new MappingConfiguration(query);
    }
}
