namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDThing : IEquatable<TDThing>, IDeserializable<TDThing>
    {
        public ArrayTracker<TDContextSpecifier>? Context { get; set; }

        public ValueTracker<StringHolder>? Id { get; set; }

        public ValueTracker<StringHolder>? Title { get; set; }

        public ArrayTracker<TDLink>? Links { get; set; }

        public MapTracker<TDDataSchema>? SchemaDefinitions { get; set; }

        public ArrayTracker<TDForm>? Forms { get; set; }

        public MapTracker<TDAction>? Actions { get; set; }

        public MapTracker<TDProperty>? Properties { get; set; }

        public MapTracker<TDEvent>? Events { get; set; }

        public virtual bool Equals(TDThing? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Context == other.Context &&
                       Id == other.Id &&
                       Title == other.Title &&
                       Links == other.Links &&
                       SchemaDefinitions == other.SchemaDefinitions &&
                       Forms == other.Forms &&
                       Actions == other.Actions &&
                       Properties == other.Properties &&
                       Events == other.Events;
            }
        }

        public override int GetHashCode()
        {
            return (Context, Id, Title, Links, SchemaDefinitions, Forms, Actions, Properties, Events).GetHashCode();
        }

        public static bool operator ==(TDThing? left, TDThing? right)
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

        public static bool operator !=(TDThing? left, TDThing? right)
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
            else if (obj is not TDThing other)
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
            if (Context != null)
            {
                foreach (ITraversable item in Context.Traverse())
                {
                    yield return item;
                }
            }
            if (Id != null)
            {
                foreach (ITraversable item in Id.Traverse())
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
            if (Links != null)
            {
                foreach (ITraversable item in Links.Traverse())
                {
                    yield return item;
                }
            }
            if (SchemaDefinitions != null)
            {
                foreach (ITraversable item in SchemaDefinitions.Traverse())
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
            if (Actions != null)
            {
                foreach (ITraversable item in Actions.Traverse())
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
            if (Events != null)
            {
                foreach (ITraversable item in Events.Traverse())
                {
                    yield return item;
                }
            }
        }

        public static TDThing Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDThing thing = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                reader.Read();

                switch (propertyName)
                {
                    case "@context":
                        thing.Context = ArrayTracker<TDContextSpecifier>.Deserialize(ref reader);
                        break;
                    case "id":
                        thing.Id = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "title":
                        thing.Title = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case "links":
                        thing.Links = ArrayTracker<TDLink>.Deserialize(ref reader);
                        break;
                    case "schemaDefinitions":
                        thing.SchemaDefinitions = MapTracker<TDDataSchema>.Deserialize(ref reader);
                        break;
                    case "forms":
                        thing.Forms = ArrayTracker<TDForm>.Deserialize(ref reader);
                        break;
                    case "actions":
                        thing.Actions = MapTracker<TDAction>.Deserialize(ref reader);
                        break;
                    case "properties":
                        thing.Properties = MapTracker<TDProperty>.Deserialize(ref reader);
                        break;
                    case "events":
                        thing.Events = MapTracker<TDEvent>.Deserialize(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            return thing;
        }
    }
}
