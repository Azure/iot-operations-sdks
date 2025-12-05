namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDEvent : IEquatable<TDEvent>, IDeserializable<TDEvent>
    {
        public const string DescriptionName = "description";
        public const string DataName = "data";
        public const string PlaceholderName = "dtv:placeholder";
        public const string FormsName = "forms";

        public ValueTracker<StringHolder>? Description { get; set; }

        public ValueTracker<TDDataSchema>? Data { get; set; }

        public ValueTracker<BoolHolder>? Placeholder { get; set; }

        public ArrayTracker<TDForm>? Forms { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

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
                       Forms == other.Forms;
            }
        }

        public override int GetHashCode()
        {
            return (Description, Data, Placeholder, Forms).GetHashCode();
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
                        evt.Description = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case DataName:
                        evt.Data = ValueTracker<TDDataSchema>.Deserialize(ref reader);
                        break;
                    case PlaceholderName:
                        evt.Placeholder = ValueTracker<BoolHolder>.Deserialize(ref reader);
                        break;
                    case FormsName:
                        evt.Forms = ArrayTracker<TDForm>.Deserialize(ref reader);
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
