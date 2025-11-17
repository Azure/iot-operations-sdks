namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDForm : IEquatable<TDForm>, IDeserializable<TDForm>
    {
        public ValueTracker<StringHolder>? Href { get; set; }

        public ValueTracker<StringHolder>? ContentType { get; set; }

        public ArrayTracker<TDSchemaReference>? AdditionalResponses { get; set; }

        public ArrayTracker<TDSchemaReference>? HeaderInfo { get; set; }

        public ValueTracker<StringHolder>? HeaderCode { get; set; }

        public ValueTracker<StringHolder>? ServiceGroupId { get; set; }

        public ValueTracker<StringHolder>? Topic { get; set; }

        public ArrayTracker<StringHolder>? Op { get; set; }

        public virtual bool Equals(TDForm? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Href == other.Href &&
                       ContentType == other.ContentType &&
                       AdditionalResponses == other.AdditionalResponses &&
                       HeaderInfo == other.HeaderInfo &&
                       HeaderCode == other.HeaderCode &&
                       ServiceGroupId == other.ServiceGroupId &&
                       Topic == other.Topic &&
                       Op == other.Op;
            }
        }

        public override int GetHashCode()
        {
            return (Href, ContentType, AdditionalResponses, HeaderInfo, HeaderCode, ServiceGroupId, Topic, Op).GetHashCode();
        }

        public static bool operator ==(TDForm? left, TDForm? right)
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

        public static bool operator !=(TDForm? left, TDForm? right)
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
            else if (obj is not TDForm other)
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
            if (ContentType != null)
            {
                foreach (ITraversable item in ContentType.Traverse())
                {
                    yield return item;
                }
            }
            if (AdditionalResponses != null)
            {
                foreach (ITraversable item in AdditionalResponses.Traverse())
                {
                    yield return item;
                }
            }
            if (HeaderInfo != null)
            {
                foreach (ITraversable item in HeaderInfo.Traverse())
                {
                    yield return item;
                }
            }
            if (HeaderCode != null)
            {
                foreach (ITraversable item in HeaderCode.Traverse())
                {
                    yield return item;
                }
            }
            if (ServiceGroupId != null)
            {
                foreach (ITraversable item in ServiceGroupId.Traverse())
                {
                    yield return item;
                }
            }
            if (Topic != null)
            {
                foreach (ITraversable item in Topic.Traverse())
                {
                    yield return item;
                }
            }
            if (Op != null)
            {
                foreach (ITraversable item in Op.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static TDForm Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDForm form = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                reader.Read();

                switch (propertyName)
                {
                    case "href":
                        form.Href = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "contentType":
                        form.ContentType = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "additionalResponses":
                        form.AdditionalResponses = ArrayTracker<TDSchemaReference>.Deserialize(ref reader);
                        break;
                    case "dtv:headerInfo":
                        form.HeaderInfo = ArrayTracker<TDSchemaReference>.Deserialize(ref reader);
                        break;
                    case "dtv:headerCode":
                        form.HeaderCode = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "dtv:serviceGroupId":
                        form.ServiceGroupId = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "dtv:topic":
                        form.Topic = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "op":
                        form.Op = ArrayTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            return form;
        }
    }
}
