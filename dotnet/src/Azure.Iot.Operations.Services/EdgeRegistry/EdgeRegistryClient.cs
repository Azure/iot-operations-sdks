// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Azure.Iot.Operations.Protocol;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Default <see cref="IEdgeRegistryClient"/> implementation, composed from the generated core, Schema,
/// Thing Description, and Thing Model extension xRegistry RPC clients (CoreClientStub, SchemaClientStub,
/// ThingDescriptionClientStub, ThingModelClientStub). It routes XID components into per-call topic
/// tokens and maps the generated wire types to the EdgeRegistry.Models domain types.
/// </summary>
/// <remarks>
/// The implementation is split across partial files, one per interface surface, so that new extensions
/// follow a consistent pattern. This file holds the shared composition: the stub fields, constructor,
/// topic-token helpers, and disposal. <c>EdgeRegistryClient.Core.cs</c> implements
/// <see cref="ICoreClient"/>; <c>EdgeRegistryClient.Schema.cs</c>,
/// <c>EdgeRegistryClient.ThingDescription.cs</c>, and <c>EdgeRegistryClient.ThingModel.cs</c> implement
/// the extension surfaces. To add an extension, add a new <c>EdgeRegistryClient.&lt;Name&gt;.cs</c>
/// partial plus its stub field, constructor wiring, and disposal here.
/// </remarks>
public sealed partial class EdgeRegistryClient : IEdgeRegistryClient
{
    private const string GroupTypeTopicToken = "groupType";
    private const string ResourceTypeTopicToken = "resourceType";
    private const string ResourceIdTopicToken = "resourceId";
    private const string VersionIdTopicToken = "versionId";
    private const string SchemaIdTopicToken = "schemaId";
    private const string ThingDescriptionIdTopicToken = "thingDescriptionId";
    private const string ThingModelIdTopicToken = "thingModelId";

    private static readonly TimeSpan s_defaultCommandTimeout = TimeSpan.FromSeconds(10);

    private readonly CoreClientStub _coreStub;
    private readonly SchemaClientStub _schemaStub;
    private readonly ThingDescriptionClientStub _thingDescriptionStub;
    private readonly ThingModelClientStub _thingModelStub;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EdgeRegistryClient"/> class.
    /// </summary>
    /// <param name="applicationContext">The shared application context.</param>
    /// <param name="mqttClient">The MQTT client used for RPC.</param>
    public EdgeRegistryClient(ApplicationContext applicationContext, IMqttPubSubClient mqttClient)
    {
        _coreStub = new CoreClientStub(applicationContext, mqttClient);
        _schemaStub = new SchemaClientStub(applicationContext, mqttClient);
        _thingDescriptionStub = new ThingDescriptionClientStub(applicationContext, mqttClient);
        _thingModelStub = new ThingModelClientStub(applicationContext, mqttClient);
    }

    // ---- Topic token helpers ----

    /// <summary>Builds the topic tokens for a Group-scoped request.</summary>
    private static Dictionary<string, string> GroupTopicTokens(string groupType)
        => new() { [GroupTypeTopicToken] = groupType };

    /// <summary>Builds the topic tokens for a Resource-scoped request.</summary>
    private static Dictionary<string, string> ResourceTopicTokens(string groupType, string resourceType, string resourceId)
        => new()
        {
            [GroupTypeTopicToken] = groupType,
            [ResourceTypeTopicToken] = resourceType,
            [ResourceIdTopicToken] = resourceId,
        };

    /// <summary>Builds the topic tokens for a Version-scoped request that carries the Version id in the topic.</summary>
    private static Dictionary<string, string> VersionTopicTokens(string groupType, string resourceType, string resourceId, string versionId)
    {
        Dictionary<string, string> tokens = ResourceTopicTokens(groupType, resourceType, resourceId);
        tokens[VersionIdTopicToken] = versionId;
        return tokens;
    }

    /// <summary>Builds the topic tokens for an extension Resource-scoped request, keyed by the extension's Resource-identifier token (e.g. <c>schemaId</c>).</summary>
    private static Dictionary<string, string> ExtensionResourceTopicTokens(string resourceIdToken, string resourceId)
        => new() { [resourceIdToken] = resourceId };

    /// <summary>Builds the topic tokens for an extension Version-scoped request that carries the Version id in the topic.</summary>
    private static Dictionary<string, string> ExtensionVersionTopicTokens(string resourceIdToken, string resourceId, ulong versionId)
    {
        Dictionary<string, string> tokens = ExtensionResourceTopicTokens(resourceIdToken, resourceId);
        tokens[VersionIdTopicToken] = versionId.ToString(CultureInfo.InvariantCulture);
        return tokens;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _coreStub.StopAsync(cancellationToken).ConfigureAwait(false);
        await _schemaStub.StopAsync(cancellationToken).ConfigureAwait(false);
        await _thingDescriptionStub.StopAsync(cancellationToken).ConfigureAwait(false);
        await _thingModelStub.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _coreStub.DisposeAsync().ConfigureAwait(false);
        await _schemaStub.DisposeAsync().ConfigureAwait(false);
        await _thingDescriptionStub.DisposeAsync().ConfigureAwait(false);
        await _thingModelStub.DisposeAsync().ConfigureAwait(false);
    }
}
