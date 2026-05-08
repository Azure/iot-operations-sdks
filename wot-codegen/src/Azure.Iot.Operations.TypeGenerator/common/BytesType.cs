// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class BytesType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Bytes; }

        internal BytesType(bool orNull)
            : base(orNull)
        {
        }
    }
}
