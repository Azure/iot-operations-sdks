// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class UnsignedIntegerType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.UnsignedInteger; }

        internal UnsignedIntegerType(bool orNull)
            : base(orNull)
        {
        }
    }
}
