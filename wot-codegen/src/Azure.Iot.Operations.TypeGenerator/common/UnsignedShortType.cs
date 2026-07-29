// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class UnsignedShortType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.UnsignedShort; }

        internal UnsignedShortType(bool orNull)
            : base(orNull)
        {
        }
    }
}
