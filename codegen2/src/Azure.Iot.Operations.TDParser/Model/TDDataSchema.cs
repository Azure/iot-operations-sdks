namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;

    public class TDDataSchema : IEquatable<TDDataSchema>, IDeserializable<TDDataSchema>
    {
        public const string RefName = "dtv:ref";
        public const string TitleName = "title";
        public const string DescriptionName = "description";
        public const string TypeName = "type";
        public const string ConstName = "const";
        public const string MinimumName = "minimum";
        public const string MaximumName = "maximum";
        public const string FormatName = "format";
        public const string PatternName = "pattern";
        public const string ContentEncodingName = "contentEncoding";
        public const string AdditionalPropertiesName = "dtv:additionalProperties";
        public const string EnumName = "enum";
        public const string RequiredName = "required";
        public const string ErrorMessageName = "dtv:errorMessage";
        public const string PropertiesName = "properties";
        public const string ItemsName = "items";

        public ValueTracker<StringHolder>? Ref { get; set; }

        public ValueTracker<StringHolder>? Title { get; set; }

        public ValueTracker<StringHolder>? Description { get; set; }

        public ValueTracker<StringHolder>? Type { get; set; }

        public ValueTracker<ObjectHolder>? Const { get; set; }

        public ValueTracker<NumberHolder>? Minimum { get; set; }

        public ValueTracker<NumberHolder>? Maximum { get; set; }

        public ValueTracker<StringHolder>? Format { get; set; }

        public ValueTracker<StringHolder>? Pattern { get; set; }

        public ValueTracker<StringHolder>? ContentEncoding { get; set; }

        public ValueTracker<TDDataSchema>? AdditionalProperties { get; set; }

        public ArrayTracker<StringHolder>? Enum { get; set; }

        public ArrayTracker<StringHolder>? Required { get; set; }

        public ValueTracker<StringHolder>? ErrorMessage { get; set; }

        public MapTracker<TDDataSchema>? Properties { get; set; }

        public ValueTracker<TDDataSchema>? Items { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public override int GetHashCode()
        {
            return (Title, Description, Type, Const, Minimum, Maximum, Format, Pattern, ContentEncoding, AdditionalProperties, Enum, Required, ErrorMessage, Properties, Items).GetHashCode();
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
                    Description == other.Description &&
                    Type == other.Type &&
                    Const == other.Const &&
                    Minimum == other.Minimum &&
                    Maximum == other.Maximum &&
                    Format == other.Format &&
                    Pattern == other.Pattern &&
                    ContentEncoding == other.ContentEncoding &&
                    ((AdditionalProperties == null && other.AdditionalProperties == null) || (AdditionalProperties?.Equals(other.AdditionalProperties) ?? false)) &&
                    ((Enum == null && other.Enum == null) || (Enum?.Elements != null && other.Enum?.Elements != null && Enum.Elements.SequenceEqual(other.Enum.Elements))) &&
                    ((Required == null && other.Required == null) || (Required?.Elements != null && other.Required?.Elements != null && Required.Elements.SequenceEqual(other.Required.Elements))) &&
                    ErrorMessage == other.ErrorMessage &&
                    Properties?.Entries?.Count == other.Properties?.Entries?.Count && (Properties == null || Properties!.Entries!.OrderBy(kv => kv.Key).SequenceEqual(other.Properties!.Entries!.OrderBy(kv => kv.Key))) &&
                    ((Items == null && other.Items == null) || (Items?.Equals(other.Items) ?? false));
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
        }

        protected static bool TryLoadPropertyValues(TDDataSchema dataSchema, string propertyName, ref Utf8JsonReader reader)
        {
            switch (propertyName)
            {
                case RefName:
                    dataSchema.Ref = ValueTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case TitleName:
                    dataSchema.Title = ValueTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case DescriptionName:
                    dataSchema.Description = ValueTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case TypeName:
                    dataSchema.Type = ValueTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case ConstName:
                    dataSchema.Const = ValueTracker<ObjectHolder>.Deserialize(ref reader);
                    return true;
                case MinimumName:
                    dataSchema.Minimum = ValueTracker<NumberHolder>.Deserialize(ref reader);
                    return true;
                case MaximumName:
                    dataSchema.Maximum = ValueTracker<NumberHolder>.Deserialize(ref reader);
                    return true;
                case FormatName:
                    dataSchema.Format = ValueTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case PatternName:
                    dataSchema.Pattern = ValueTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case ContentEncodingName:
                    dataSchema.ContentEncoding = ValueTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case AdditionalPropertiesName:
                    dataSchema.AdditionalProperties = ValueTracker<TDDataSchema>.Deserialize(ref reader);
                    return true;
                case EnumName:
                    dataSchema.Enum = ArrayTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case RequiredName:
                    dataSchema.Required = ArrayTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case ErrorMessageName:
                    dataSchema.ErrorMessage = ValueTracker<StringHolder>.Deserialize(ref reader);
                    return true;
                case PropertiesName:
                    dataSchema.Properties = MapTracker<TDDataSchema>.Deserialize(ref reader);
                    return true;
                case ItemsName:
                    dataSchema.Items = ValueTracker<TDDataSchema>.Deserialize(ref reader);
                    return true;
                default:
                    return false;
            }
        }
    }
}
