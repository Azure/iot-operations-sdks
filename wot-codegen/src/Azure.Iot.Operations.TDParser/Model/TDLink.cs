// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDLink : IEquatable<TDLink>, IDeserializable<TDLink>
    {
        public const string HrefName = TDCommon.HrefName;
        public const string TypeName = "type";
        public const string RelName = "rel";
        public const string RefNameName = "dov:refName";
        public const string RefNameLegacyName = "aov:refName";
        public const string RefTypeName = "dov:refType";
        public const string RefTypeLegacyName = "aov:refType";

        public static readonly HashSet<string> SupportedProperties = new()
        {
            HrefName,
            TypeName,
            RelName,
            RefNameName,
            RefNameLegacyName,
            RefTypeName,
            RefTypeLegacyName
        };

        public ValueTracker<StringHolder>? Href { get; set; }

        public ValueTracker<StringHolder>? Type { get; set; }

        public ValueTracker<StringHolder>? Rel { get; set; }

        public ValueTracker<StringHolder>? RefName { get; set; }

        public ValueTracker<StringHolder>? RefType { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public PrefixType RefNamePrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType RefTypePrefixType { get; set; } = PrefixType.Indeterminate;

        public virtual bool Equals(TDLink? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Href == other.Href && Type == other.Type && Rel == other.Rel && RefName == other.RefName && RefType == other.RefType;
            }
        }

        public override int GetHashCode()
        {
            return (Href, Type, Rel, RefName, RefType).GetHashCode();
        }

        public static bool operator ==(TDLink? left, TDLink? right)
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

        public static bool operator !=(TDLink? left, TDLink? right)
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
            else if (obj is not TDLink other)
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
            if (Href != null)
            {
                foreach (ITraversable item in Href.Traverse())
                {
                    yield return item;
                }
            }
            if (Type != null)
            {
                foreach (ITraversable item in Type.Traverse())
                {
                    yield return item;
                }
            }
            if (Rel != null)
            {
                foreach (ITraversable item in Rel.Traverse())
                {
                    yield return item;
                }
            }
            if (RefName != null)
            {
                foreach (ITraversable item in RefName.Traverse())
                {
                    yield return item;
                }
            }
            if (RefType != null)
            {
                foreach (ITraversable item in RefType.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static TDLink Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDLink link = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, link.PropertyNames, "link");
                link.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                switch (propertyName)
                {
                    case HrefName:
                        link.Href = ValueTracker<StringHolder>.Deserialize(ref reader, HrefName);
                        break;
                    case TypeName:
                        link.Type = ValueTracker<StringHolder>.Deserialize(ref reader, TypeName);
                        break;
                    case RelName:
                        link.Rel = ValueTracker<StringHolder>.Deserialize(ref reader, RelName);
                        break;
                    case RefNameName:
                        link.RefName = ValueTracker<StringHolder>.Deserialize(ref reader, RefNameName);
                        link.RefNamePrefixType = PrefixType.DoVocabulary;
                        break;
                    case RefNameLegacyName:
                        link.RefName = ValueTracker<StringHolder>.Deserialize(ref reader, RefNameName);
                        link.RefNamePrefixType = PrefixType.AioPlatform;
                        break;
                    case RefTypeName:
                        link.RefType = ValueTracker<StringHolder>.Deserialize(ref reader, RefTypeName);
                        link.RefTypePrefixType = PrefixType.DoVocabulary;
                        break;
                    case RefTypeLegacyName:
                        link.RefType = ValueTracker<StringHolder>.Deserialize(ref reader, RefTypeName);
                        link.RefTypePrefixType = PrefixType.AioPlatform;
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            return link;
        }
    }
}
