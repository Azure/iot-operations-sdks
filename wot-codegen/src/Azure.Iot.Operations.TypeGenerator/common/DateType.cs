// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class DateType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Date; }

        internal DateType(bool orNull)
            : base(orNull)
        {
        }
    }
}
