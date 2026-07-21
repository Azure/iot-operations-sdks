// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    public class TDDataSchema : IEquatable<TDDataSchema>, IDeserializable<TDDataSchema>
    {
        public const string RefName = "dov:ref";
        public const string RefLegacyName = "dtv:ref";
        public const string LocalRefName = "tm:ref";
        public const string TitleName = TDCommon.TitleName;
        public const string DescriptionName = TDCommon.DescriptionName;
        public const string TypeName = "type";
        public const string ConstName = "const";
        public const string MinimumName = "minimum";
        public const string MaximumName = "maximum";
        public const string ScaleFactorName = "dov:scaleFactor";
        public const string ScaleFactorLegacyName = "aov:scaleFactor";
        public const string DecimalPlacesName = "dov:decimalPlaces";
        public const string DecimalPlacesLegacyName = "aov:decimalPlaces";
        public const string FormatName = "format";
        public const string PatternName = "pattern";
        public const string ContentEncodingName = "contentEncoding";
        public const string AdditionalPropertiesName = "dov:additionalProperties";
        public const string AdditionalPropertiesLegacyName = "dtv:additionalProperties";
        public const string EnumName = "enum";
        public const string RequiredName = "required";
        public const string ErrorMessageName = "dov:errorMessage";
        public const string ErrorMessageLegacyName = "dtv:errorMessage";
        public const string PropertiesName = "properties";
        public const string ItemsName = "items";
        public const string TypeRefName = "dov:typeRef";
        public const string TypeRefLegacyName = "aov:typeRef";
        public const string NamespaceName = TDCommon.NamespaceName;
        public const string NamespaceLegacyName = TDCommon.NamespaceLegacyName;

        public static readonly HashSet<string> SupportedProperties = new()
        {
            RefName,
            RefLegacyName,
            LocalRefName,
            TitleName,
            DescriptionName,
            TypeName,
            ConstName,
            MinimumName,
            MaximumName,
            ScaleFactorName,
            ScaleFactorLegacyName,
            DecimalPlacesName,
            DecimalPlacesLegacyName,
            FormatName,
            PatternName,
            ContentEncodingName,
            AdditionalPropertiesName,
            AdditionalPropertiesLegacyName,
            EnumName,
            RequiredName,
            ErrorMessageName,
            ErrorMessageLegacyName,
            PropertiesName,
            ItemsName,
            TypeRefName,
            TypeRefLegacyName,
            NamespaceName,
            NamespaceLegacyName
        };

        public ValueTracker<StringHolder>? Ref { get; set; }

        public ValueTracker<StringHolder>? LocalRef { get; set; }

        public ValueTracker<StringHolder>? Title { get; set; }

        public ValueTracker<StringHolder>? Description { get; set; }

        public ValueTracker<StringHolder>? Type { get; set; }

        public ValueTracker<ObjectHolder>? Const { get; set; }

        public ValueTracker<NumberHolder>? Minimum { get; set; }

        public ValueTracker<NumberHolder>? Maximum { get; set; }

        public ValueTracker<NumberHolder>? ScaleFactor { get; set; }

        public ValueTracker<NumberHolder>? DecimalPlaces { get; set; }

        public ValueTracker<StringHolder>? Format { get; set; }

        public ValueTracker<StringHolder>? Pattern { get; set; }

        public ValueTracker<StringHolder>? ContentEncoding { get; set; }

        public ValueTracker<TDDataSchema>? AdditionalProperties { get; set; }

        public ArrayTracker<StringHolder>? Enum { get; set; }

        public ArrayTracker<StringHolder>? Required { get; set; }

        public ValueTracker<StringHolder>? ErrorMessage { get; set; }

        public MapTracker<TDDataSchema>? Properties { get; set; }

        public ValueTracker<TDDataSchema>? Items { get; set; }

        public ValueTracker<StringHolder>? TypeRef { get; set; }

        public ValueTracker<StringHolder>? Namespace { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public PrefixType RefPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType ScaleFactorPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType DecimalPlacesPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType AdditionalPropertiesPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType ErrorMessagePrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType TypeRefPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType NamespacePrefixType { get; set; } = PrefixType.Indeterminate;

        public override int GetHashCode()
        {
            return (Ref, LocalRef, Title, Description, Type, Const, Minimum, Maximum, ScaleFactor, DecimalPlaces, Format, Pattern, ContentEncoding, AdditionalProperties, Enum, Required, ErrorMessage, Properties, Items, TypeRef, Namespace).GetHashCode();
        }

        public virtual bool Equals(TDDataSchema? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Title == other.Title &&
                    Ref == other.Ref &&
                    LocalRef == other.LocalRef &&
                    Description == other.Description &&
                    Type == other.Type &&
                    Const == other.Const &&
                    Minimum == other.Minimum &&
                    Maximum == other.Maximum &&
                    ScaleFactor == other.ScaleFactor &&
                    DecimalPlaces == other.DecimalPlaces &&
                    Format == other.Format &&
                    Pattern == other.Pattern &&
                    ContentEncoding == other.ContentEncoding &&
                    ((AdditionalProperties == null && other.AdditionalProperties == null) || (AdditionalProperties?.Equals(other.AdditionalProperties) ?? false)) &&
                    ((Enum == null && other.Enum == null) || (Enum?.Elements != null && other.Enum?.Elements != null && Enum.Elements.SequenceEqual(other.Enum.Elements))) &&
                    ((Required == null && other.Required == null) || (Required?.Elements != null && other.Required?.Elements != null && Required.Elements.SequenceEqual(other.Required.Elements))) &&
                    ErrorMessage == other.ErrorMessage &&
                    Properties?.Entries?.Count == other.Properties?.Entries?.Count && (Properties == null || Properties!.Entries!.OrderBy(kv => kv.Key).SequenceEqual(other.Properties!.Entries!.OrderBy(kv => kv.Key))) &&
                    ((Items == null && other.Items == null) || (Items?.Equals(other.Items) ?? false)) &&
                    TypeRef == other.TypeRef &&
                    Namespace == other.Namespace;
            }
        }

        public static bool operator ==(TDDataSchema? left, TDDataSchema? right)
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

        public static bool operator !=(TDDataSchema? left, TDDataSchema? right)
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
            else if (obj is not TDDataSchema other)
            {
                return false;
            }
            else
            {
                return Equals(other);
            }
        }

        public static TDDataSchema Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDDataSchema dataSchema = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, dataSchema.PropertyNames, "data schema");
                dataSchema.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                if (!TryLoadPropertyValues(dataSchema, propertyName, ref reader))
                {
                    reader.Skip();
                }

                reader.Read();
            }

            return dataSchema;
        }

        public virtual IEnumerable<ITraversable> Traverse()
        {
            if (Ref != null)
            {
                foreach (ITraversable item in Ref.Traverse())
                {
                    yield return item;
                }
            }
            if (LocalRef != null)
            {
                foreach (ITraversable item in LocalRef.Traverse())
                {
                    yield return item;
                }
            }
            if (Title != null)
            {
                foreach (ITraversable item in Title.Traverse())
                {
                    yield return item;
                }
            }
            if (Description != null)
            {
                foreach (ITraversable item in Description.Traverse())
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
            if (Const != null)
            {
                foreach (ITraversable item in Const.Traverse())
                {
                    yield return item;
                }
            }
            if (Minimum != null)
            {
                foreach (ITraversable item in Minimum.Traverse())
                {
                    yield return item;
                }
            }
            if (Maximum != null)
            {
                foreach (ITraversable item in Maximum.Traverse())
                {
                    yield return item;
                }
            }
            if (ScaleFactor != null)
            {
                foreach (ITraversable item in ScaleFactor.Traverse())
                {
                    yield return item;
                }
            }
            if (DecimalPlaces != null)
            {
                foreach (ITraversable item in DecimalPlaces.Traverse())
                {
                    yield return item;
                }
            }
            if (Format != null)
            {
                foreach (ITraversable item in Format.Traverse())
                {
                    yield return item;
                }
            }
            if (Pattern != null)
            {
                foreach (ITraversable item in Pattern.Traverse())
                {
                    yield return item;
                }
            }
            if (ContentEncoding != null)
            {
                foreach (ITraversable item in ContentEncoding.Traverse())
                {
                    yield return item;
                }
            }
            if (AdditionalProperties != null)
            {
                foreach (ITraversable item in AdditionalProperties.Traverse())
                {
                    yield return item;
                }
            }
            if (Enum != null)
            {
                foreach (ITraversable item in Enum.Traverse())
                {
                    yield return item;
                }
            }
            if (Required != null)
            {
                foreach (ITraversable item in Required.Traverse())
                {
                    yield return item;
                }
            }
            if (ErrorMessage != null)
            {
                foreach (ITraversable item in ErrorMessage.Traverse())
                {
                    yield return item;
                }
            }
            if (Properties != null)
            {
                foreach (ITraversable item in Properties.Traverse())
                {
                    yield return item;
                }
            }
            if (Items != null)
            {
                foreach (ITraversable item in Items.Traverse())
                {
                    yield return item;
                }
            }
            if (TypeRef != null)
            {
                foreach (ITraversable item in TypeRef.Traverse())
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
        }

        protected static bool TryLoadPropertyValues(TDDataSchema dataSchema, string propertyName, ref Utf8JsonReader reader)
        {
            switch (propertyName)
            {
                case RefName:
                    dataSchema.Ref = ValueTracker<StringHolder>.Deserialize(ref reader, RefName);
                    dataSchema.RefPrefixType = PrefixType.DoVocabulary;
                    return true;
                case RefLegacyName:
                    dataSchema.Ref = ValueTracker<StringHolder>.Deserialize(ref reader, RefName);
                    dataSchema.RefPrefixType = PrefixType.AioProtocol;
                    return true;
                case LocalRefName:
                    dataSchema.LocalRef = ValueTracker<StringHolder>.Deserialize(ref reader, LocalRefName);
                    return true;
                case TitleName:
                    dataSchema.Title = ValueTracker<StringHolder>.Deserialize(ref reader, TitleName);
                    return true;
                case DescriptionName:
                    dataSchema.Description = ValueTracker<StringHolder>.Deserialize(ref reader, DescriptionName);
                    return true;
                case TypeName:
                    dataSchema.Type = ValueTracker<StringHolder>.Deserialize(ref reader, TypeName);
                    return true;
                case ConstName:
                    dataSchema.Const = ValueTracker<ObjectHolder>.Deserialize(ref reader, ConstName);
                    return true;
                case MinimumName:
                    dataSchema.Minimum = ValueTracker<NumberHolder>.Deserialize(ref reader, MinimumName);
                    return true;
                case MaximumName:
                    dataSchema.Maximum = ValueTracker<NumberHolder>.Deserialize(ref reader, MaximumName);
                    return true;
                case ScaleFactorName:
                    dataSchema.ScaleFactor = ValueTracker<NumberHolder>.Deserialize(ref reader, ScaleFactorName);
                    dataSchema.ScaleFactorPrefixType = PrefixType.DoVocabulary;
                    return true;
                case ScaleFactorLegacyName:
                    dataSchema.ScaleFactor = ValueTracker<NumberHolder>.Deserialize(ref reader, ScaleFactorName);
                    dataSchema.ScaleFactorPrefixType = PrefixType.AioPlatform;
                    return true;
                case DecimalPlacesName:
                    dataSchema.DecimalPlaces = ValueTracker<NumberHolder>.Deserialize(ref reader, DecimalPlacesName);
                    dataSchema.DecimalPlacesPrefixType = PrefixType.DoVocabulary;
                    return true;
                case DecimalPlacesLegacyName:
                    dataSchema.DecimalPlaces = ValueTracker<NumberHolder>.Deserialize(ref reader, DecimalPlacesName);
                    dataSchema.DecimalPlacesPrefixType = PrefixType.AioPlatform;
                    return true;
                case FormatName:
                    dataSchema.Format = ValueTracker<StringHolder>.Deserialize(ref reader, FormatName);
                    return true;
                case PatternName:
                    dataSchema.Pattern = ValueTracker<StringHolder>.Deserialize(ref reader, PatternName);
                    return true;
                case ContentEncodingName:
                    dataSchema.ContentEncoding = ValueTracker<StringHolder>.Deserialize(ref reader, ContentEncodingName);
                    return true;
                case AdditionalPropertiesName:
                    dataSchema.AdditionalProperties = ValueTracker<TDDataSchema>.Deserialize(ref reader, AdditionalPropertiesName);
                    dataSchema.AdditionalPropertiesPrefixType = PrefixType.DoVocabulary;
                    return true;
                case AdditionalPropertiesLegacyName:
                    dataSchema.AdditionalProperties = ValueTracker<TDDataSchema>.Deserialize(ref reader, AdditionalPropertiesName);
                    dataSchema.AdditionalPropertiesPrefixType = PrefixType.AioProtocol;
                    return true;
                case EnumName:
                    dataSchema.Enum = ArrayTracker<StringHolder>.Deserialize(ref reader, EnumName);
                    return true;
                case RequiredName:
                    dataSchema.Required = ArrayTracker<StringHolder>.Deserialize(ref reader, RequiredName);
                    return true;
                case ErrorMessageName:
                    dataSchema.ErrorMessage = ValueTracker<StringHolder>.Deserialize(ref reader, ErrorMessageName);
                    dataSchema.ErrorMessagePrefixType = PrefixType.DoVocabulary;
                    return true;
                case ErrorMessageLegacyName:
                    dataSchema.ErrorMessage = ValueTracker<StringHolder>.Deserialize(ref reader, ErrorMessageName);
                    dataSchema.ErrorMessagePrefixType = PrefixType.AioProtocol;
                    return true;
                case PropertiesName:
                    dataSchema.Properties = MapTracker<TDDataSchema>.Deserialize(ref reader, PropertiesName);
                    return true;
                case ItemsName:
                    dataSchema.Items = ValueTracker<TDDataSchema>.Deserialize(ref reader, ItemsName);
                    return true;
                case TypeRefName:
                    dataSchema.TypeRef = ValueTracker<StringHolder>.Deserialize(ref reader, TypeRefName);
                    dataSchema.TypeRefPrefixType = PrefixType.DoVocabulary;
                    return true;
                case TypeRefLegacyName:
                    dataSchema.TypeRef = ValueTracker<StringHolder>.Deserialize(ref reader, TypeRefName);
                    dataSchema.TypeRefPrefixType = PrefixType.AioPlatform;
                    return true;
                case NamespaceName:
                    dataSchema.Namespace = ValueTracker<StringHolder>.Deserialize(ref reader, NamespaceName);
                    dataSchema.NamespacePrefixType = PrefixType.DoVocabulary;
                    return true;
                case NamespaceLegacyName:
                    dataSchema.Namespace = ValueTracker<StringHolder>.Deserialize(ref reader, NamespaceName);
                    dataSchema.NamespacePrefixType = PrefixType.AioPlatform;
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryGetLocalRefSchemaKey(string localRefValue, [NotNullWhen(true)] out string? schemaKey, [NotNullWhen(false)] out string? error)
        {
            const string Prefix = "#/schemaDefinitions/";

            schemaKey = null;
            error = null;

            if (!localRefValue.StartsWith(Prefix, StringComparison.Ordinal))
            {
                error = $"Data schema '{LocalRefName}' property value \"{localRefValue}\" must be a local JSON Pointer of the form \"{Prefix}<escaped-key>\".";
                return false;
            }

            string encodedKey = localRefValue[Prefix.Length..];
            if (encodedKey.Length == 0)
            {
                error = $"Data schema '{LocalRefName}' property value \"{localRefValue}\" must identify a key in '{TDThing.SchemaDefinitionsName}'.";
                return false;
            }

            if (encodedKey.Contains('/'))
            {
                error = $"Data schema '{LocalRefName}' property value \"{localRefValue}\" must identify exactly one key in '{TDThing.SchemaDefinitionsName}'.";
                return false;
            }

            if (!TryDecodeJsonPointerSegment(encodedKey, out schemaKey, out error))
            {
                error = $"Data schema '{LocalRefName}' property value \"{localRefValue}\" is invalid: {error}";
                return false;
            }

            return true;
        }

        private static bool TryDecodeJsonPointerSegment(string encodedSegment, [NotNullWhen(true)] out string? decodedSegment, [NotNullWhen(false)] out string? error)
        {
            StringBuilder builder = new();

            for (int index = 0; index < encodedSegment.Length; index++)
            {
                char current = encodedSegment[index];
                if (current != '~')
                {
                    builder.Append(current);
                    continue;
                }

                if (index + 1 >= encodedSegment.Length)
                {
                    decodedSegment = null;
                    error = "a '~' escape sequence is incomplete";
                    return false;
                }

                char escape = encodedSegment[index + 1];
                switch (escape)
                {
                    case '0':
                        builder.Append('~');
                        break;
                    case '1':
                        builder.Append('/');
                        break;
                    default:
                        decodedSegment = null;
                        error = $"escape sequence '~{escape}' is not supported";
                        return false;
                }

                index++;
            }

            decodedSegment = builder.ToString();
            error = null;
            return true;
        }
    }
}
