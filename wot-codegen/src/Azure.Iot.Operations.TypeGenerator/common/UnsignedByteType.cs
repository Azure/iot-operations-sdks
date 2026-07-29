// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class UnsignedByteType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.UnsignedByte; }

        internal UnsignedByteType(bool orNull)
            : base(orNull)
        {
        }
    }
}
