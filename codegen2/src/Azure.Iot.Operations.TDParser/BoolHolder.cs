// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class BoolHolder : BaseHolder<bool>, IEquatable<BoolHolder>, IDeserializable<BoolHolder>
    {
        public virtual bool Equals(BoolHolder? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Value == other.Value;
            }
        }

        public static BoolHolder Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False)
            {
                throw new InvalidOperationException($"expected JSON bool but found {reader.TokenType}");
            }

            return new BoolHolder { Value = reader.GetBoolean()! };
        }

        public IEnumerable<ITraversable> Traverse()
        {
            yield break;
        }
    }
}
