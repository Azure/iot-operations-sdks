// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TypeGenerator
{
    internal abstract class SchemaType
    {
        internal SchemaType(bool orNull)
        {
            OrNull = orNull;
        }

        internal abstract SchemaKind Kind { get; }

        internal bool OrNull { get; }
    }
}
