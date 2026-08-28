// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

namespace Azure.Iot.Operations.Services.EdgeRegistry;

/// <summary>
/// Derives Resource identifiers that satisfy the cloud naming rules from arbitrary identifiers.
/// </summary>
public static class ResourceId
{
    /// <summary>
    /// The label key under which the original, pre-derivation identifier is recorded by
    /// <see cref="Derive(string, IReadOnlyList{Models.Label}?)"/>.
    /// </summary>
    /// <remarks>
    /// The label belongs on the Resource, and a lookup filters on this key with the original
    /// identifier as the value, not the derived Resource identifier.
    /// </remarks>
    public const string OriginalIdLabelKey = "originalid";

    private const int MinLength = 3;
    private const int MaxLength = 64;

    /// <summary>
    /// Derives a Resource identifier from an arbitrary identifier, recording the original in
    /// <paramref name="resourceLabels"/> under <see cref="OriginalIdLabelKey"/> and returning both.
    /// </summary>
    /// <param name="originalId">The identifier to derive a Resource identifier from.</param>
    /// <param name="resourceLabels">The Resource labels to record the original identifier in. Left unmodified; the returned list is a copy.</param>
    /// <returns>The derived Resource identifier and the labels recording <paramref name="originalId"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="originalId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="originalId"/> is empty.</exception>
    /// <remarks>
    /// <paramref name="originalId"/> is used as the Resource identifier unchanged when it already
    /// satisfies the cloud naming rules: 3 to 64 characters drawn from <c>[a-z0-9-]</c>, beginning and
    /// ending with an alphanumeric. Every other identifier is hashed, yielding the lowercase hex
    /// SHA-256 of <paramref name="originalId"/>. A hashed identifier is always 64 characters drawn from
    /// <c>[0-9a-f]</c>, which satisfies both the xRegistry identifier rules and the stricter cloud rules.
    /// <para>
    /// An identical <see cref="OriginalIdLabelKey"/> entry is never duplicated, while entries recording
    /// a <em>different</em> identifier are left in place.
    /// </para>
    /// </remarks>
    public static (string ResourceId, IReadOnlyList<Models.Label> ResourceLabels) Derive(string originalId, IReadOnlyList<Models.Label>? resourceLabels = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalId);

        string resourceId = ConformsToCloudNamingRules(originalId)
            ? originalId
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(originalId))).ToLowerInvariant();

        List<Models.Label> labels = resourceLabels is null
            ? new List<Models.Label>()
            : resourceLabels.Where(label => !(label.Key == OriginalIdLabelKey && label.Value == originalId)).ToList();
        labels.Add(new Models.Label { Key = OriginalIdLabelKey, Value = originalId });

        return (resourceId, labels);
    }

    /// <summary>
    /// Returns whether <paramref name="id"/> satisfies the cloud naming rules: 3 to 64 characters
    /// drawn from <c>[a-z0-9-]</c>, beginning and ending with an alphanumeric.
    /// </summary>
    private static bool ConformsToCloudNamingRules(string id)
        => id.Length is >= MinLength and <= MaxLength
            && id.All(c => IsLowercaseAlphanumeric(c) || c == '-')
            && IsLowercaseAlphanumeric(id[0])
            && IsLowercaseAlphanumeric(id[^1]);

    private static bool IsLowercaseAlphanumeric(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9';
}
