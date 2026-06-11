// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDProperty : TDDataSchema, IEquatable<TDProperty>, IDeserializable<TDProperty>
    {
        public const string ReadOnlyName = "readOnly";
        public const string PlaceholderName = "dov:placeholder";
        public const string PlaceholderLegacyName = "dtv:placeholder";
        public const string FormsName = TDCommon.FormsName;
        public const string ContainsName = TDCommon.ContainsName;
        public const string ContainsLegacyName = TDCommon.ContainsLegacyName;
        public const string ContainedInName = TDCommon.ContainedInName;
        public const string ContainedInLegacyName = TDCommon.ContainedInLegacyName;
        public const string WithUnitName = TDCommon.WithUnitName;
        public const string WithUnitLegacyName = TDCommon.WithUnitLegacyName;
        public const string HasQuantityKindName = TDCommon.HasQuantityKindName;
        public const string MemberOfName = TDCommon.MemberOfName;
        public const string MemberOfLegacyName = TDCommon.MemberOfLegacyName;
        public const string PropertyIriName = TDCommon.PropertyIriName;
        public const string PropertyConfigurationName = "dov:propertyConfiguration";

        public static new readonly HashSet<string> SupportedProperties = new()
        {
            ReadOnlyName,
            PlaceholderName,
            PlaceholderLegacyName,
            FormsName,
            ContainsName,
            ContainsLegacyName,
            ContainedInName,
            ContainedInLegacyName,
            WithUnitName,
            WithUnitLegacyName,
            HasQuantityKindName,
            MemberOfName,
            MemberOfLegacyName,
            PropertyIriName,
            PropertyConfigurationName
        };

        public ValueTracker<BoolHolder>? ReadOnly { get; set; }

        public ValueTracker<BoolHolder>? Placeholder { get; set; }

        public ArrayTracker<TDForm>? Forms { get; set; }

        public ArrayTracker<StringHolder>? Contains { get; set; }

        public ValueTracker<StringHolder>? ContainedIn { get; set; }

        public ValueTracker<StringHolder>? WithUnit { get; set; }

        public ValueTracker<StringHolder>? HasQuantityKind { get; set; }

        public ValueTracker<StringHolder>? MemberOf { get; set; }

        public ValueTracker<StringHolder>? PropertyIri { get; set; }

        public ValueTracker<TDAnything>? PropertyConfiguration { get; set; }

        public PrefixType PlaceholderPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType ContainsPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType ContainedInPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType WithUnitPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType MemberOfPrefixType { get; set; } = PrefixType.Indeterminate;

        public virtual bool Equals(TDProperty? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return base.Equals(other) &&
                       ReadOnly == other.ReadOnly &&
                       Placeholder == other.Placeholder &&
                       Forms == other.Forms &&
                       Contains == other.Contains &&
                       ContainedIn == other.ContainedIn &&
                       WithUnit == other.WithUnit &&
                       HasQuantityKind == other.HasQuantityKind &&
                       MemberOf == other.MemberOf &&
                       PropertyIri == other.PropertyIri &&
                       PropertyConfiguration == other.PropertyConfiguration;
            }
        }

        public override int GetHashCode()
        {
            return (base.GetHashCode(), ReadOnly, Placeholder, Forms, Contains, ContainedIn, WithUnit, HasQuantityKind, MemberOf, PropertyIri, PropertyConfiguration).GetHashCode();
        }

        public static bool operator ==(TDProperty? left, TDProperty? right)
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

        public static bool operator !=(TDProperty? left, TDProperty? right)
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
            else if (obj is not TDProperty other)
            {
                return false;
            }
            else
            {
                return Equals(other);
            }
        }

        public override IEnumerable<ITraversable> Traverse()
        {
            foreach (ITraversable baseChild in base.Traverse())
            {
                yield return baseChild;
            }

            if (ReadOnly != null)
            {
                foreach (ITraversable item in ReadOnly.Traverse())
                {
                    yield return item;
                }
            }
            if (Placeholder != null)
            {
                foreach (ITraversable item in Placeholder.Traverse())
                {
                    yield return item;
                }
            }
            if (Forms != null)
            {
                foreach (ITraversable item in Forms.Traverse())
                {
                    yield return item;
                }
            }
            if (Contains != null)
            {
                foreach (ITraversable item in Contains.Traverse())
                {
                    yield return item;
                }
            }
            if (ContainedIn != null)
            {
                foreach (ITraversable item in ContainedIn.Traverse())
                {
                    yield return item;
                }
            }
            if (WithUnit != null)
            {
                foreach (ITraversable item in WithUnit.Traverse())
                {
                    yield return item;
                }
            }
            if (HasQuantityKind != null)
            {
                foreach (ITraversable item in HasQuantityKind.Traverse())
                {
                    yield return item;
                }
            }
            if (MemberOf != null)
            {
                foreach (ITraversable item in MemberOf.Traverse())
                {
                    yield return item;
                }
            }
            if (PropertyIri != null)
            {
                foreach (ITraversable item in PropertyIri.Traverse())
                {
                    yield return item;
                }
            }
            if (PropertyConfiguration != null)
            {
                foreach (ITraversable item in PropertyConfiguration.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static new TDProperty Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDProperty prop = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, prop.PropertyNames, "property");
                prop.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                if (!TryLoadPropertyValues(prop, propertyName, ref reader))
                {
                    switch (propertyName)
                    {
                        case ReadOnlyName:
                            prop.ReadOnly = ValueTracker<BoolHolder>.Deserialize(ref reader, ReadOnlyName);
                            break;
                        case PlaceholderName:
                            prop.Placeholder = ValueTracker<BoolHolder>.Deserialize(ref reader, PlaceholderName);
                            prop.PlaceholderPrefixType = PrefixType.DoVocabulary;
                            break;
                        case PlaceholderLegacyName:
                            prop.Placeholder = ValueTracker<BoolHolder>.Deserialize(ref reader, PlaceholderName);
                            prop.PlaceholderPrefixType = PrefixType.AioProtocol;
                            break;
                        case FormsName:
                            prop.Forms = ArrayTracker<TDForm>.Deserialize(ref reader, FormsName);
                            break;
                        case ContainsName:
                            prop.Contains = ArrayTracker<StringHolder>.Deserialize(ref reader, ContainsName);
                            prop.ContainsPrefixType = PrefixType.DoVocabulary;
                            break;
                        case ContainsLegacyName:
                            prop.Contains = ArrayTracker<StringHolder>.Deserialize(ref reader, ContainsName);
                            prop.ContainsPrefixType = PrefixType.AioPlatform;
                            break;
                        case ContainedInName:
                            prop.ContainedIn = ValueTracker<StringHolder>.Deserialize(ref reader, ContainedInName);
                            prop.ContainedInPrefixType = PrefixType.DoVocabulary;
                            break;
                        case ContainedInLegacyName:
                            prop.ContainedIn = ValueTracker<StringHolder>.Deserialize(ref reader, ContainedInName);
                            prop.ContainedInPrefixType = PrefixType.AioPlatform;
                            break;
                        case WithUnitName:
                            prop.WithUnit = ValueTracker<StringHolder>.Deserialize(ref reader, WithUnitName);
                            prop.WithUnitPrefixType = PrefixType.DoVocabulary;
                            break;
                        case WithUnitLegacyName:
                            prop.WithUnit = ValueTracker<StringHolder>.Deserialize(ref reader, WithUnitName);
                            prop.WithUnitPrefixType = PrefixType.AioPlatform;
                            break;
                        case HasQuantityKindName:
                            prop.HasQuantityKind = ValueTracker<StringHolder>.Deserialize(ref reader, HasQuantityKindName);
                            break;
                        case MemberOfName:
                            prop.MemberOf = ValueTracker<StringHolder>.Deserialize(ref reader, MemberOfName);
                            prop.MemberOfPrefixType = PrefixType.DoVocabulary;
                            break;
                        case MemberOfLegacyName:
                            prop.MemberOf = ValueTracker<StringHolder>.Deserialize(ref reader, MemberOfName);
                            prop.MemberOfPrefixType = PrefixType.AioPlatform;
                            break;
                        case PropertyIriName:
                            prop.PropertyIri = ValueTracker<StringHolder>.Deserialize(ref reader, PropertyIriName);
                            break;
                        case PropertyConfigurationName:
                            prop.PropertyConfiguration = ValueTracker<TDAnything>.Deserialize(ref reader, PropertyConfigurationName);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                reader.Read();
            }

            return prop;
        }
    }
}
