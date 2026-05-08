// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class BooleanType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Boolean; }

        internal BooleanType(bool orNull)
            : base(orNull)
        {
        }
    }
}
