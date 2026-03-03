// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class IntegerType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Integer; }

        internal IntegerType(bool orNull)
            : base(orNull)
        {
        }
    }
}
