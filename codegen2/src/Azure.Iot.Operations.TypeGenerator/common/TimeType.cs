// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal class TimeType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Time; }

        internal TimeType(bool orNull)
            : base(orNull)
        {
        }
    }
}
