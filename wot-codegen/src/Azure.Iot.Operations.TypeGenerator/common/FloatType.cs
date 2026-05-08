// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class FloatType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Float; }

        internal FloatType(bool orNull)
            : base(orNull)
        {
        }
    }
}
