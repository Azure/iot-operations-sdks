// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class UuidType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Uuid; }

        internal UuidType(bool orNull)
            : base(orNull)
        {
        }
    }
}
