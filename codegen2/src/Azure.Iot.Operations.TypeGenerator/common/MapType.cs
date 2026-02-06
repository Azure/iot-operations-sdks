// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class MapType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Map; }

        internal MapType(SchemaType valueSchema, bool orNull)
            : base(orNull)
        {
            ValueSchema = valueSchema;
        }

        internal SchemaType ValueSchema { get; }
    }
}
