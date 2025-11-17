namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDSchemaReference : IEquatable<TDSchemaReference>, IDeserializable<TDSchemaReference>
    {
        public ValueTracker<BoolHolder>? Success { get; set; }

        public ValueTracker<StringHolder>? ContentType { get; set; }

        public ValueTracker<StringHolder>? Schema { get; set; }

        public virtual bool Equals(TDSchemaReference? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Success == other.Success && ContentType == other.ContentType && Schema == other.Schema;
            }
        }

        public override int GetHashCode()
        {
            return (Success, ContentType, Schema).GetHashCode();
        }

        public static bool operator ==(TDSchemaReference? left, TDSchemaReference? right)
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

        public static bool operator !=(TDSchemaReference? left, TDSchemaReference? right)
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
            else if (obj is not TDSchemaReference other)
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
            if (Success != null)
            {
                foreach (ITraversable item in Success.Traverse())
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
            if (Schema != null)
            {
                foreach (ITraversable item in Schema.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static TDSchemaReference Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDSchemaReference schemaRef = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                reader.Read();

                switch (propertyName)
                {
                    case "success":
                        schemaRef.Success = ValueTracker<BoolHolder>.Deserialize(ref reader);
                        break;
                    case "contentType":
                        schemaRef.ContentType = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "schema":
                        schemaRef.Schema = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            return schemaRef;
        }
    }
}
