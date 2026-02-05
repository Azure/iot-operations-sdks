// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    internal class EnumType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Enum; }

        internal EnumType(CodeName schemaName, string? description, CodeName[] enumValues, bool orNull)
            : base(orNull)
        {
            SchemaName = schemaName;
            Description = description;
            EnumValues = enumValues;
        }

        internal CodeName SchemaName { get; }

        internal string? Description { get; }

        internal CodeName[] EnumValues { get; }
    }
}
