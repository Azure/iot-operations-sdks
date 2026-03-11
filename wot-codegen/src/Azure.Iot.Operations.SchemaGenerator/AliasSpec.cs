// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.SchemaGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal record AliasSpec(string? Description, string? Type, string Ref, SerializationFormat Format, string SchemaName, string Base, long TokenIndex) : SchemaSpec(Format, TokenIndex);
}
