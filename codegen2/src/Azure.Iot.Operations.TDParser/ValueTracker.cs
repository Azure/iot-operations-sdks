namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class ValueTracker<T> : IEquatable<ValueTracker<T>>, ISourceTracker
        where T : IDeserializable<T>
    {
        public required T Value { get; set; }

        public bool DeserializingFailed { get; set; }

        public string? DeserializationError { get; set; }

        public long TokenIndex { get; set; } = -1;

        public virtual bool Equals(ValueTracker<T>? other)
        {
            if (other == null)
            {
                return false;
            }
            else if (Value == null || other.Value == null)
            {
                return Value == null && other.Value == null;
            }
            else
            {
                return Value.Equals(other.Value);
            }
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(ValueTracker<T>? left, ValueTracker<T>? right)
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

        public static bool operator !=(ValueTracker<T>? left, ValueTracker<T>? right)
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
            else if (obj is not ValueTracker<T> other)
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
            yield return this;

            if (Value != null)
            {
                foreach (ITraversable item in Value.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static ValueTracker<T> Deserialize(ref Utf8JsonReader reader)
        {
            long tokenIndex = reader.TokenStartIndex;

            try
            {
                T value = T.Deserialize(ref reader);
                return new ValueTracker<T>
                {
                    Value = value,
                    TokenIndex = tokenIndex,
                };
            }
            catch (Exception ex)
            {
                reader.Skip();

                return new ValueTracker<T>
                {
                    Value = default!,
                    DeserializingFailed = true,
                    DeserializationError = ex.Message,
                    TokenIndex = tokenIndex,
                };
            }
        }
    }
}
