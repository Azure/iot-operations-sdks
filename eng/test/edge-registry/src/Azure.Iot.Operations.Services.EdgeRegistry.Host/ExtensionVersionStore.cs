// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Host;

using System.Security.Cryptography;
using Azure.Iot.Operations.Protocol.RPC;
using Azure.Iot.Operations.Services.EdgeRegistry.Host.Generated;
using static Azure.Iot.Operations.Services.EdgeRegistry.Host.ExtensionStub;

/// <summary>
/// Shared in-memory store for an xRegistry extension surface (Schema / Thing Description / Thing Model).
/// Resources are keyed by (groupId, resourceId); Versions use service-assigned integer identifiers and
/// the newest Version is the Resource's default. The store is generic over the surface's create-attributes
/// type so all three extensions reuse the same Create / Get / List / Delete behavior.
/// </summary>
/// <typeparam name="TAttributes">The generated create-version attributes type for the surface.</typeparam>
internal sealed class ExtensionVersionStore<TAttributes>(Func<TAttributes, List<Label>> versionLabels, Func<TAttributes, byte[]> versionDocument)
{
    private readonly object _gate = new();
    private readonly Dictionary<(string GroupId, string ResourceId), StoredResource> _resources = new();

    public StoredVersion CreateVersion(string groupId, string resourceId, IReadOnlyList<Label> parentLabels, TAttributes attributes)
    {
        lock (_gate)
        {
            DateTime now = DateTime.UtcNow;
            if (!_resources.TryGetValue((groupId, resourceId), out StoredResource? resource))
            {
                resource = new StoredResource(groupId, resourceId);
                _resources[(groupId, resourceId)] = resource;
            }

            resource.ParentLabels = parentLabels.Select(l => new Label { Key = l.Key, Value = l.Value }).ToList();
            StoredVersion version = new(resource.NextVersionId++, attributes, now) { Owner = resource };
            resource.Versions.Add(version);
            resource.DefaultVersionId = version.VersionId; // newest Version becomes the default
            return version;
        }
    }

    public StoredVersion? GetVersion(string groupId, string resourceId, ulong? versionId)
    {
        lock (_gate)
        {
            if (!_resources.TryGetValue((groupId, resourceId), out StoredResource? resource))
            {
                return null;
            }

            ulong target = versionId ?? resource.DefaultVersionId;
            return resource.Versions.FirstOrDefault(v => v.VersionId == target);
        }
    }

    public List<(string GroupId, string ResourceId, ulong VersionId)> ListVersions(string? groupId, bool allGroups, string? resourceId, string? documentHash, Label? label)
    {
        lock (_gate)
        {
            List<(string, string, ulong)> result = new();
            foreach (StoredResource resource in _resources.Values)
            {
                if (!allGroups && resource.GroupId != groupId)
                {
                    continue;
                }

                if (resourceId is not null && resource.ResourceId != resourceId)
                {
                    continue;
                }

                foreach (StoredVersion version in resource.Versions)
                {
                    if (label is not null && !versionLabels(version.Attributes).Any(l => l.Key == label.Key && l.Value == label.Value))
                    {
                        continue;
                    }

                    if (documentHash is not null)
                    {
                        string hash = ComputeHash(versionDocument(version.Attributes));
                        if (!string.Equals(hash, documentHash, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    result.Add((resource.GroupId, resource.ResourceId, version.VersionId));
                }
            }

            return result;
        }
    }

    // TODO: for correct behavior, this should return an error if the item isn't found or the expected epoch doesn't match.
    public void DeleteVersion(string groupId, string resourceId, ulong versionId, ulong? expectedEpoch)
    {
        lock (_gate)
        {
            if (_resources.TryGetValue((groupId, resourceId), out StoredResource? resource))
            {
                resource.Versions.RemoveAll(v => v.VersionId == versionId && (expectedEpoch is null || v.Epoch == expectedEpoch));
                if (resource.DefaultVersionId == versionId)
                {
                    resource.DefaultVersionId = resource.Versions.Count > 0 ? resource.Versions[^1].VersionId : 0;
                }
            }
        }
    }

    internal sealed class StoredResource(string groupId, string resourceId)
    {
        public string GroupId { get; } = groupId;

        public string ResourceId { get; } = resourceId;

        public List<Label> ParentLabels { get; set; } = new();

        public List<StoredVersion> Versions { get; } = new();

        public ulong DefaultVersionId { get; set; }

        public ulong NextVersionId { get; set; } = 1;
    }

    internal sealed class StoredVersion(ulong versionId, TAttributes attributes, DateTime createdAt)
    {
        public ulong VersionId { get; } = versionId;

        public TAttributes Attributes { get; } = attributes;

        public DateTime CreatedAt { get; } = createdAt;

        public DateTime ModifiedAt { get; set; } = createdAt;

        public ulong Epoch { get; } = 1;

        public required StoredResource Owner { get; init; }

        public bool IsDefault => Owner.DefaultVersionId == VersionId;
    }
}

/// <summary>Helpers shared by the in-memory extension services.</summary>
internal static class ExtensionStub
{
    public const string DefaultGroupId = "default";
    public const string VersionIdToken = "ex:versionId";

    public static string TopicToken(CommandRequestMetadata metadata, string token)
        => metadata.TopicTokens.TryGetValue(token, out string? value)
            ? value
            : throw new InvalidOperationException($"Required topic token '{token}' was not present on the request.");

    public static ExtendedResponse<T> Ok<T>(T response)
        where T : class
        => new() { Response = response };

    public static List<Label> CloneLabels(List<Label> labels)
        => labels.Select(l => new Label { Key = l.Key, Value = l.Value }).ToList();

    public static string ComputeHash(byte[] document)
        => Convert.ToHexString(SHA256.HashData(document));
}
