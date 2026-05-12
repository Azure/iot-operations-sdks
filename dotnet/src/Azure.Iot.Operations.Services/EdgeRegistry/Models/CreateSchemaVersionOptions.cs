// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.EdgeRegistry.Generated;

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Options for creating a schema version.
/// </summary>
public class CreateSchemaVersionOptions
{
    /// <summary>
    /// Schema format identifier, e.g. <see cref="SchemaFormat.JsonSchemaDraft07"/> or <see cref="SchemaFormat.Avro1110"/>.
    /// </summary>
    public string Format { get; set; } = SchemaFormat.JsonSchemaDraft07;

    /// <summary>
    /// The raw schema document content.
    /// </summary>
    public byte[] SchemaDocument { get; set; } = default!;

    /// <summary>
    /// The versionId of this version's ancestor if it has an ancestor.
    /// </summary>
    public ulong? Ancestor { get; set; }

    /// <summary>
    /// Content type of the schema version.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Description of the schema version.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Documentation URL or text.
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// Labels for the schema version.
    /// </summary>
    public Dictionary<string, string> Labels { get; set; } = new();

    /// <summary>
    /// Human-readable name.
    /// </summary>
    public string? Name { get; set; }
}
