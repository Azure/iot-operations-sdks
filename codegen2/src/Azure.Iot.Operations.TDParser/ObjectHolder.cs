namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class ObjectHolder : IEquatable<ObjectHolder>, IDeserializable<ObjectHolder>
    {
        public required object Value { get; set; }

        public virtual bool Equals(ObjectHolder? other)
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

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(ObjectHolder? left, ObjectHolder? right)
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

        public static bool operator !=(ObjectHolder? left, ObjectHolder? right)
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
            else if (obj is not ObjectHolder other)
            {
                return false;
            }
            else
            {
                return Equals(other);
            }
        }

        public static ObjectHolder Deserialize(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return new ObjectHolder { Value = reader.GetString()! };
                case JsonTokenType.Number:
                    return new ObjectHolder { Value = reader.GetDouble() };
                case JsonTokenType.True:
                    return new ObjectHolder { Value = true };
                case JsonTokenType.False:
                    return new ObjectHolder { Value = false };
                default:
                    throw new InvalidOperationException($"expected primitive value but found {reader.TokenType}");
            }
        }

        public IEnumerable<ITraversable> Traverse()
        {
            yield break;
        }
    }
}
