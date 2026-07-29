// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Format of a Thing Description Version document. The known format mirrors the one defined by the
/// xRegistry Thing Description extension; any other identifier can be supplied via
/// <see cref="Custom(string)"/>.
/// </summary>
public readonly struct ThingDescriptionFormat
{
    private ThingDescriptionFormat(string value) => Value = value;

    /// <summary>JSON-LD 1.1 format (<c>JSON-LD/1.1</c>).</summary>
    public static ThingDescriptionFormat JsonLd11 => new(Generated.ThingDescriptionFormat.JsonLd11);

    /// <summary>A format identifier not covered by the known formats.</summary>
    public static ThingDescriptionFormat Custom(string format) => new(format);

    /// <summary>Implicitly treats a string as a (possibly custom) format identifier.</summary>
    public static implicit operator ThingDescriptionFormat(string format) => new(format);

    /// <summary>The wire format identifier.</summary>
    public string Value { get; }
}
