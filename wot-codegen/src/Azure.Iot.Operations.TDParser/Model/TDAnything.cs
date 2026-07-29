// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDAnything : IEquatable<TDAnything>, IDeserializable<TDAnything>
    {
        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public virtual bool Equals(TDAnything? other)
        {
            return other != null;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public static bool operator ==(TDAnything? left, TDAnything? right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                return left.Equals(right);
            }
        }

        public static bool operator !=(TDAnything? left, TDAnything? right)
        {
            return !(left == right);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else if (ReferenceEquals(obj, null))
            {
                return false;
            }
            else if (obj is not TDAnything other)
            {
                return false;
            }
            else
            {
                return Equals(other);
            }
        }

        public IEnumerable<ITraversable> Traverse()
        {
            yield break;
        }

        public static TDAnything Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDAnything anything = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, anything.PropertyNames, "object");
                anything.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();
                reader.Skip();
                reader.Read();
            }

            return anything;
        }
    }
}
