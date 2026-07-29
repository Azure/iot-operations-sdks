// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class DurationType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Duration; }

        internal DurationType(bool orNull)
            : base(orNull)
        {
        }
    }
}
