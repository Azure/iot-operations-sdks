// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDAffordanceGroup : IEquatable<TDAffordanceGroup>, IDeserializable<TDAffordanceGroup>
    {
        public const string TitleName = TDCommon.TitleName;

        public static readonly HashSet<string> SupportedProperties = new()
        {
            TitleName
        };

        public ValueTracker<StringHolder>? Title { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public virtual bool Equals(TDAffordanceGroup? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Title == other.Title;
            }
        }

        public override int GetHashCode()
        {
            return Title?.GetHashCode() ?? 0;
        }

        public static bool operator ==(TDAffordanceGroup? left, TDAffordanceGroup? right)
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

        public static bool operator !=(TDAffordanceGroup? left, TDAffordanceGroup? right)
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
            else if (obj is not TDAffordanceGroup other)
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
            if (Title != null)
            {
                foreach (ITraversable item in Title.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static TDAffordanceGroup Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDAffordanceGroup group = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, group.PropertyNames, "affordance group");
                group.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                switch (propertyName)
                {
                    case TitleName:
                        group.Title = ValueTracker<StringHolder>.Deserialize(ref reader, TitleName);
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            return group;
        }
    }
}
