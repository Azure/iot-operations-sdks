namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDThing : IEquatable<TDThing>, IDeserializable<TDThing>
    {
        public const string ContextName = "@context";
        public const string TypeName = "@type";
        public const string TitleName = "title";
        public const string LinksName = "links";
        public const string SchemaDefinitionsName = "schemaDefinitions";
        public const string FormsName = "forms";
        public const string ActionsName = "actions";
        public const string PropertiesName = "properties";
        public const string EventsName = "events";

        public ArrayTracker<TDContextSpecifier>? Context { get; set; }

        public ValueTracker<StringHolder>? Type { get; set; }

        public ValueTracker<StringHolder>? Title { get; set; }

        public ArrayTracker<TDLink>? Links { get; set; }

        public MapTracker<TDDataSchema>? SchemaDefinitions { get; set; }

        public ArrayTracker<TDForm>? Forms { get; set; }

        public MapTracker<TDAction>? Actions { get; set; }

        public MapTracker<TDProperty>? Properties { get; set; }

        public MapTracker<TDEvent>? Events { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public virtual bool Equals(TDThing? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Context == other.Context &&
                       Type == other.Type &&
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
            return (Context, Type, Title, Links, SchemaDefinitions, Forms, Actions, Properties, Events).GetHashCode();
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
            if (Type != null)
            {
                foreach (ITraversable item in Type.Traverse())
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
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, thing.PropertyNames, "thing");
                thing.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                switch (propertyName)
                {
                    case ContextName:
                        thing.Context = ArrayTracker<TDContextSpecifier>.Deserialize(ref reader, ContextName);
                        break;
                    case TypeName:
                        thing.Type = ValueTracker<StringHolder>.Deserialize(ref reader, TypeName);
                        break;
                    case TitleName:
                        thing.Title = ValueTracker<StringHolder>.Deserialize(ref reader, TitleName);
                        break;
                    case LinksName:
                        thing.Links = ArrayTracker<TDLink>.Deserialize(ref reader, LinksName);
                        break;
                    case SchemaDefinitionsName:
                        thing.SchemaDefinitions = MapTracker<TDDataSchema>.Deserialize(ref reader, SchemaDefinitionsName);
                        break;
                    case FormsName:
                        thing.Forms = ArrayTracker<TDForm>.Deserialize(ref reader, FormsName);
                        break;
                    case ActionsName:
                        thing.Actions = MapTracker<TDAction>.Deserialize(ref reader, ActionsName);
                        break;
                    case PropertiesName:
                        thing.Properties = MapTracker<TDProperty>.Deserialize(ref reader, PropertiesName);
                        break;
                    case EventsName:
                        thing.Events = MapTracker<TDEvent>.Deserialize(ref reader, EventsName);
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
