namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class ArrayTracker<T> : IEquatable<ArrayTracker<T>>, ISourceTracker
        where T : IDeserializable<T>
    {
        public List<ValueTracker<T>>? Elements { get; set; }

        public bool DeserializingFailed { get => false; }

        public string? DeserializationError { get => null; }

        public long TokenIndex { get; set; }

        public virtual bool Equals(ArrayTracker<T>? other)
        {
            if (other == null)
            {
                return false;
            }
            if (Elements == null && other.Elements == null)
            {
                return true;
            }
            if (Elements == null || other.Elements == null)
            {
                return false;
            }
            if (Elements.Count != other.Elements.Count)
            {
                return false;
            }
            for (int i = 0; i < Elements.Count; i++)
            {
                if (Elements[i] != other.Elements[i])
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            return Elements != null ? Elements.GetHashCode() : 0;
        }

        public static bool operator ==(ArrayTracker<T>? left, ArrayTracker<T>? right)
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

        public static bool operator !=(ArrayTracker<T>? left, ArrayTracker<T>? right)
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
            else if (obj is not ArrayTracker<T> other)
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

            if (Elements != null)
            {
                foreach (ValueTracker<T> element in Elements)
                {
                    foreach (ITraversable item in element.Traverse())
                    {
                        yield return item;
                    }
                }
            }
        }

        public static ArrayTracker<T> Deserialize(ref Utf8JsonReader reader, string propertyName)
        {
            long tokenIndex = reader.TokenStartIndex;

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                ValueTracker<T> valueTracker = ValueTracker<T>.Deserialize(ref reader, propertyName);

                return new ArrayTracker<T>
                {
                    Elements = new List<ValueTracker<T>> { valueTracker },
                    TokenIndex = tokenIndex,
                };
            }

            List<ValueTracker<T>> elements = new();

            reader.Read();
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                elements.Add(ValueTracker<T>.Deserialize(ref reader, propertyName));
                reader.Read();
            }

            return new ArrayTracker<T>
            {
                Elements = elements,
                TokenIndex = tokenIndex,
            };
        }
    }
}
