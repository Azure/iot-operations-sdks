namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class MapTracker<T> : IEquatable<MapTracker<T>>, ISourceTracker
        where T : IDeserializable<T>
    {
        public Dictionary<string, ValueTracker<T>>? Entries { get; set; }

        public bool DeserializingFailed { get; set; }

        public string? DeserializationError { get; set; }

        public long TokenIndex { get; set; }

        public virtual bool Equals(MapTracker<T>? other)
        {
            if (other == null)
            {
                return false;
            }
            if (Entries == null && other.Entries == null)
            {
                return true;
            }
            if (Entries == null || other.Entries == null)
            {
                return false;
            }
            if (Entries.Count != other.Entries.Count)
            {
                return false;
            }
            foreach (var kvp in Entries)
            {
                if (!other.Entries.TryGetValue(kvp.Key, out var otherValue) || kvp.Value != otherValue)
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            return Entries != null ? Entries.GetHashCode() : 0;
        }

        public static bool operator ==(MapTracker<T>? left, MapTracker<T>? right)
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

        public static bool operator !=(MapTracker<T>? left, MapTracker<T>? right)
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
            else if (obj is not MapTracker<T> other)
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

            if (Entries != null)
            {
                foreach (ValueTracker<T> entryValue in Entries.Values)
                {
                    foreach (ITraversable item in entryValue.Traverse())
                    {
                        yield return item;
                    }
                }
            }
        }

        public static MapTracker<T> Deserialize(ref Utf8JsonReader reader)
        {
            long tokenIndex = reader.TokenStartIndex;
            string? deserializationError = null;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return new MapTracker<T>
                {
                    DeserializingFailed = true,
                    DeserializationError = $"expected JSON object but found {reader.TokenType}",
                    TokenIndex = tokenIndex,
                };
            }

            Dictionary<string, ValueTracker<T>> entries = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                reader.Read();

                if (entries.ContainsKey(propertyName))
                {
                    deserializationError = $"duplicate property name '{propertyName}' found in map";
                }

                entries[propertyName] = ValueTracker<T>.Deserialize(ref reader);
                reader.Read();
            }

            return new MapTracker<T>
            {
                DeserializingFailed = deserializationError != null,
                DeserializationError = deserializationError,
                Entries = entries,
                TokenIndex = tokenIndex,
            };
        }
    }
}
