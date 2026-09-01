// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// The XID components that identify a Version.
/// </summary>
public class VersionXId
{
    private const string VersionsSegment = "versions";
    private const int SegmentCount = 6;

    /// <summary>
    /// Group type.
    /// </summary>
    public required string GroupType { get; set; }

    /// <summary>
    /// Group identifier.
    /// </summary>
    public required string GroupId { get; set; }

    /// <summary>
    /// Resource type.
    /// </summary>
    public required string ResourceType { get; set; }

    /// <summary>
    /// Resource identifier.
    /// </summary>
    public required string ResourceId { get; set; }

    /// <summary>
    /// Version identifier.
    /// </summary>
    public required string VersionId { get; set; }

    /// <summary>
    /// Parses a full XID path of the form
    /// <c>/{groupType}/{groupId}/{resourceType}/{resourceId}/versions/{versionId}</c> into its components.
    /// </summary>
    /// <param name="xid">The XID path to parse. A leading <c>/</c> is optional.</param>
    /// <returns>The parsed components.</returns>
    /// <exception cref="FormatException"><paramref name="xid"/> isn't a Version XID path.</exception>
    public static VersionXId Parse(string xid)
    {
        ArgumentNullException.ThrowIfNull(xid);

        string[] segments = xid.TrimStart('/').Split('/');
        if (segments.Length != SegmentCount
            || segments[4] != VersionsSegment
            || Array.Exists(segments, segment => segment.Length == 0))
        {
            throw new FormatException($"Invalid Version XID format: {xid}");
        }

        return new VersionXId
        {
            GroupType = segments[0],
            GroupId = segments[1],
            ResourceType = segments[2],
            ResourceId = segments[3],
            VersionId = segments[5],
        };
    }
}
