namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDLink : IEquatable<TDLink>, IDeserializable<TDLink>
    {
        public ValueTracker<StringHolder>? Href { get; set; }

        public ValueTracker<StringHolder>? ContentType { get; set; }

        public ValueTracker<StringHolder>? Rel { get; set; }

        public virtual bool Equals(TDLink? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Href == other.Href && ContentType == other.ContentType && Rel == other.Rel;
            }
        }

        public override int GetHashCode()
        {
            return (Href, ContentType, Rel).GetHashCode();
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
            if (ContentType != null)
            {
                foreach (ITraversable item in ContentType.Traverse())
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
                reader.Read();

                switch (propertyName)
                {
                    case "href":
                        link.Href = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "contentType":
                        link.ContentType = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "rel":
                        link.Rel = ValueTracker<StringHolder>.Deserialize(ref reader);
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
