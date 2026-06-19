// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Format of a Schema Version document. The known formats mirror those defined by the xRegistry
/// Schema extension; any other identifier can be supplied via <see cref="Custom(string)"/>.
/// </summary>
public readonly struct SchemaFormat
{
    private SchemaFormat(string value) => Value = value;

    /// <summary>JSON Schema Draft-07 format (<c>JsonSchema/draft-07</c>).</summary>
    public static SchemaFormat JsonSchemaDraft07 => new("JsonSchema/draft-07");

    /// <summary>Avro 1.11.0 format (<c>Avro/1.11.0</c>).</summary>
    public static SchemaFormat Avro1110 => new("Avro/1.11.0");

    /// <summary>A format identifier not covered by the known formats.</summary>
    public static SchemaFormat Custom(string format) => new(format);

    /// <summary>Implicitly treats a string as a (possibly custom) format identifier.</summary>
    public static implicit operator SchemaFormat(string format) => new(format);

    /// <summary>The wire format identifier.</summary>
    public string Value { get; }
}
