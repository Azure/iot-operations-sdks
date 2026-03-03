// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDContextSpecifier : IEquatable<TDContextSpecifier>, IDeserializable<TDContextSpecifier>
    {
        public ValueTracker<StringHolder>? Remote { get; set; }

        public MapTracker<StringHolder>? Local { get; set; }

        public virtual bool Equals(TDContextSpecifier? other)
        {
            if (other == null)
            {
                return false;
            }
            if (Remote != other.Remote)
            {
                return false;
            }
            if (Local != other.Local)
            {
                return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return (Remote, Local).GetHashCode();
        }

        public static bool operator ==(TDContextSpecifier? left, TDContextSpecifier? right)
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

        public static bool operator !=(TDContextSpecifier? left, TDContextSpecifier? right)
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
            else if (obj is not TDContextSpecifier other)
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
            if (Remote != null)
            {
                foreach (ITraversable item in Remote.Traverse())
                {
                    yield return item;
                }
            }
            if (Local != null)
            {
                foreach (ITraversable item in Local.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static TDContextSpecifier Deserialize(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    return new TDContextSpecifier
                    {
                        Local = MapTracker<StringHolder>.Deserialize(ref reader, string.Empty),
                    };
                case JsonTokenType.String:
                    return new TDContextSpecifier
                    {
                        Remote = ValueTracker<StringHolder>.Deserialize(ref reader, string.Empty),
                    };
                default:
                    throw new InvalidOperationException($"expected string or JSON object but found {reader.TokenType}");
            }
        }
    }
}
