// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class NumberHolder : BaseHolder<double>, IEquatable<NumberHolder>, IDeserializable<NumberHolder>
    {
        public virtual bool Equals(NumberHolder? other)
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

        public static NumberHolder Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new InvalidOperationException($"expected JSON number but found {reader.TokenType}");
            }

            return new NumberHolder { Value = reader.GetDouble() };
        }

        public IEnumerable<ITraversable> Traverse()
        {
            yield break;
        }
    }
}
