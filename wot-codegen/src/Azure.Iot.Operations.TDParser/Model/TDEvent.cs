// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDEvent : IEquatable<TDEvent>, IDeserializable<TDEvent>
    {
        public const string DescriptionName = TDCommon.DescriptionName;
        public const string DataName = "data";
        public const string PlaceholderName = "dov:placeholder";
        public const string PlaceholderLegacyName = "dtv:placeholder";
        public const string FormsName = TDCommon.FormsName;
        public const string ContainsName = TDCommon.ContainsName;
        public const string ContainsLegacyName = TDCommon.ContainsLegacyName;
        public const string ContainedInName = TDCommon.ContainedInName;
        public const string ContainedInLegacyName = TDCommon.ContainedInLegacyName;
        public const string NamespaceName = TDCommon.NamespaceName;
        public const string NamespaceLegacyName = TDCommon.NamespaceLegacyName;
        public const string WithUnitName = TDCommon.WithUnitName;
        public const string WithUnitLegacyName = TDCommon.WithUnitLegacyName;
        public const string HasQuantityKindName = TDCommon.HasQuantityKindName;
        public const string MemberOfName = TDCommon.MemberOfName;
        public const string MemberOfLegacyName = TDCommon.MemberOfLegacyName;
        public const string PropertyIriName = TDCommon.PropertyIriName;
        public const string EventConfigurationName = "dov:eventConfiguration";

        public static readonly HashSet<string> SupportedProperties = new()
        {
            DescriptionName,
            DataName,
            PlaceholderName,
            PlaceholderLegacyName,
            FormsName,
            ContainsName,
            ContainsLegacyName,
            ContainedInName,
            ContainedInLegacyName,
            NamespaceName,
            NamespaceLegacyName,
            WithUnitName,
            WithUnitLegacyName,
            HasQuantityKindName,
            MemberOfName,
            MemberOfLegacyName,
            PropertyIriName,
            EventConfigurationName
        };

        public ValueTracker<StringHolder>? Description { get; set; }

        public ValueTracker<TDDataSchema>? Data { get; set; }

        public ValueTracker<BoolHolder>? Placeholder { get; set; }

        public ArrayTracker<TDForm>? Forms { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public ArrayTracker<StringHolder>? Contains { get; set; }

        public ValueTracker<StringHolder>? ContainedIn { get; set; }

        public ValueTracker<StringHolder>? Namespace { get; set; }

        public ValueTracker<StringHolder>? WithUnit { get; set; }

        public ValueTracker<StringHolder>? HasQuantityKind { get; set; }

        public ValueTracker<StringHolder>? MemberOf { get; set; }

        public ValueTracker<StringHolder>? PropertyIri { get; set; }

        public ValueTracker<TDAnything>? EventConfiguration { get; set; }

        public PrefixType PlaceholderPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType ContainsPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType ContainedInPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType NamespacePrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType WithUnitPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType MemberOfPrefixType { get; set; } = PrefixType.Indeterminate;

        public virtual bool Equals(TDEvent? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Description == other.Description &&
                       Data == other.Data &&
                       Placeholder == other.Placeholder &&
                       Forms == other.Forms &&
                       Contains == other.Contains &&
                       ContainedIn == other.ContainedIn &&
                       Namespace == other.Namespace &&
                       WithUnit == other.WithUnit &&
                       HasQuantityKind == other.HasQuantityKind &&
                       MemberOf == other.MemberOf &&
                       PropertyIri == other.PropertyIri &&
                       EventConfiguration == other.EventConfiguration;
            }
        }

        public override int GetHashCode()
        {
            return (Description, Data, Placeholder, Forms, Contains, ContainedIn, Namespace, WithUnit, HasQuantityKind, MemberOf, PropertyIri, EventConfiguration).GetHashCode();
        }

        public static bool operator ==(TDEvent? left, TDEvent? right)
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

        public static bool operator !=(TDEvent? left, TDEvent? right)
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
            else if (obj is not TDEvent other)
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
            if (Description != null)
            {
                foreach (ITraversable item in Description.Traverse())
                {
                    yield return item;
                }
            }
            if (Data != null)
            {
                foreach (ITraversable item in Data.Traverse())
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
            if (Namespace != null)
            {
                foreach (ITraversable item in Namespace.Traverse())
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
            if (EventConfiguration != null)
            {
                foreach (ITraversable item in EventConfiguration.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static TDEvent Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDEvent evt = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, evt.PropertyNames, "event");
                evt.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                switch (propertyName)
                {
                    case DescriptionName:
                        evt.Description = ValueTracker<StringHolder>.Deserialize(ref reader, DescriptionName);
                        break;
                    case DataName:
                        evt.Data = ValueTracker<TDDataSchema>.Deserialize(ref reader, DataName);
                        break;
                    case PlaceholderName:
                        evt.Placeholder = ValueTracker<BoolHolder>.Deserialize(ref reader, PlaceholderName);
                        evt.PlaceholderPrefixType = PrefixType.DoVocabulary;
                        break;
                    case PlaceholderLegacyName:
                        evt.Placeholder = ValueTracker<BoolHolder>.Deserialize(ref reader, PlaceholderName);
                        evt.PlaceholderPrefixType = PrefixType.AioProtocol;
                        break;
                    case FormsName:
                        evt.Forms = ArrayTracker<TDForm>.Deserialize(ref reader, FormsName);
                        break;
                    case ContainsName:
                        evt.Contains = ArrayTracker<StringHolder>.Deserialize(ref reader, ContainsName);
                        evt.ContainsPrefixType = PrefixType.DoVocabulary;
                        break;
                    case ContainsLegacyName:
                        evt.Contains = ArrayTracker<StringHolder>.Deserialize(ref reader, ContainsName);
                        evt.ContainsPrefixType = PrefixType.AioPlatform;
                        break;
                    case ContainedInName:
                        evt.ContainedIn = ValueTracker<StringHolder>.Deserialize(ref reader, ContainedInName);
                        evt.ContainedInPrefixType = PrefixType.DoVocabulary;
                        break;
                    case ContainedInLegacyName:
                        evt.ContainedIn = ValueTracker<StringHolder>.Deserialize(ref reader, ContainedInName);
                        evt.ContainedInPrefixType = PrefixType.AioPlatform;
                        break;
                    case NamespaceName:
                        evt.Namespace = ValueTracker<StringHolder>.Deserialize(ref reader, NamespaceName);
                        evt.NamespacePrefixType = PrefixType.DoVocabulary;
                        break;
                    case NamespaceLegacyName:
                        evt.Namespace = ValueTracker<StringHolder>.Deserialize(ref reader, NamespaceName);
                        evt.NamespacePrefixType = PrefixType.AioPlatform;
                        break;
                    case WithUnitName:
                        evt.WithUnit = ValueTracker<StringHolder>.Deserialize(ref reader, WithUnitName);
                        evt.WithUnitPrefixType = PrefixType.DoVocabulary;
                        break;
                    case WithUnitLegacyName:
                        evt.WithUnit = ValueTracker<StringHolder>.Deserialize(ref reader, WithUnitName);
                        evt.WithUnitPrefixType = PrefixType.AioPlatform;
                        break;
                    case HasQuantityKindName:
                        evt.HasQuantityKind = ValueTracker<StringHolder>.Deserialize(ref reader, HasQuantityKindName);
                        break;
                    case MemberOfName:
                        evt.MemberOf = ValueTracker<StringHolder>.Deserialize(ref reader, MemberOfName);
                        evt.MemberOfPrefixType = PrefixType.DoVocabulary;
                        break;
                    case MemberOfLegacyName:
                        evt.MemberOf = ValueTracker<StringHolder>.Deserialize(ref reader, MemberOfName);
                        evt.MemberOfPrefixType = PrefixType.AioPlatform;
                        break;
                    case PropertyIriName:
                        evt.PropertyIri = ValueTracker<StringHolder>.Deserialize(ref reader, PropertyIriName);
                        break;
                    case EventConfigurationName:
                        evt.EventConfiguration = ValueTracker<TDAnything>.Deserialize(ref reader, EventConfigurationName);
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            return evt;
        }
    }
}
