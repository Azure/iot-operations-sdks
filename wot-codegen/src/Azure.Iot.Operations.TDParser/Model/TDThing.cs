// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDThing : IEquatable<TDThing>, IDeserializable<TDThing>
    {
        public const string ContextName = "@context";
        public const string TypeName = "@type";
        public const string TitleName = TDCommon.TitleName;
        public const string DescriptionName = TDCommon.DescriptionName;
        public const string LinksName = "links";
        public const string SchemaDefinitionsName = "schemaDefinitions";
        public const string FormsName = TDCommon.FormsName;
        public const string OptionalName = "tm:optional";
        public const string ActionsName = "actions";
        public const string PropertiesName = "properties";
        public const string EventsName = "events";
        public const string SecurityDefinitionsName = "securityDefinitions";
        public const string SecurityName = "security";
        public const string IsCompositeName = "dov:isComposite";
        public const string IsCompositeLegacyName = "aov:isComposite";
        public const string IsEventName = "dov:isEvent";
        public const string IsEventLegacyName = "aov:isEvent";
        public const string TypeRefName = "dov:typeRef";
        public const string TypeRefLegacyName = "aov:typeRef";
        public const string MetadataName = "dov:metadata";
        public const string PropertyGroupsName = "dov:propertyGroups";
        public const string EventGroupsName = "dov:eventGroups";
        public const string ActionGroupsName = "dov:actionGroups";

        public static readonly HashSet<string> SupportedProperties = new()
        {
            ContextName,
            TypeName,
            TitleName,
            DescriptionName,
            LinksName,
            SchemaDefinitionsName,
            FormsName,
            OptionalName,
            ActionsName,
            PropertiesName,
            EventsName,
            SecurityDefinitionsName,
            SecurityName,
            IsCompositeName,
            IsCompositeLegacyName,
            IsEventName,
            IsEventLegacyName,
            TypeRefName,
            TypeRefLegacyName,
            MetadataName,
            PropertyGroupsName,
            EventGroupsName,
            ActionGroupsName
        };

        public ArrayTracker<TDContextSpecifier>? Context { get; set; }

        public ValueTracker<StringHolder>? Type { get; set; }

        public ValueTracker<StringHolder>? Title { get; set; }

        public ValueTracker<StringHolder>? Description { get; set; }

        public ArrayTracker<TDLink>? Links { get; set; }

        public MapTracker<TDDataSchema>? SchemaDefinitions { get; set; }

        public ArrayTracker<TDForm>? Forms { get; set; }

        public ArrayTracker<StringHolder>? Optional { get; set; }

        public MapTracker<TDAction>? Actions { get; set; }

        public MapTracker<TDProperty>? Properties { get; set; }

        public MapTracker<TDEvent>? Events { get; set; }

        public MapTracker<TDAnything>? SecurityDefinitions { get; set; }

        public ValueTracker<StringHolder>? Security { get; set; }

        public ValueTracker<BoolHolder>? IsComposite { get; set; }

        public ValueTracker<BoolHolder>? IsEvent { get; set; }

        public ValueTracker<StringHolder>? TypeRef { get; set; }

        public ValueTracker<TDAnything>? Metadata { get; set; }

        public ArrayTracker<TDAffordanceGroup>? PropertyGroups { get; set; }

        public ArrayTracker<TDAffordanceGroup>? EventGroups { get; set; }

        public ArrayTracker<TDAffordanceGroup>? ActionGroups { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public PrefixType IsCompositePrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType IsEventPrefixType { get; set; } = PrefixType.Indeterminate;

        public PrefixType TypeRefPrefixType { get; set; } = PrefixType.Indeterminate;

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
                       Description == other.Description &&
                       Links == other.Links &&
                       SchemaDefinitions == other.SchemaDefinitions &&
                       Forms == other.Forms &&
                       Optional == other.Optional &&
                       Actions == other.Actions &&
                       Properties == other.Properties &&
                       Events == other.Events &&
                       SecurityDefinitions == other.SecurityDefinitions &&
                       Security == other.Security &&
                       IsComposite == other.IsComposite &&
                       IsEvent == other.IsEvent &&
                       TypeRef == other.TypeRef &&
                       Metadata == other.Metadata &&
                       PropertyGroups == other.PropertyGroups &&
                       EventGroups == other.EventGroups &&
                       ActionGroups == other.ActionGroups;
            }
        }

        public override int GetHashCode()
        {
            return (Context, Type, Title, Description, Links, SchemaDefinitions, Forms, Optional, Actions, Properties, Events, SecurityDefinitions, Security, IsComposite, IsEvent, TypeRef, Metadata, PropertyGroups, EventGroups, ActionGroups).GetHashCode();
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
            if (Description != null)
            {
                foreach (ITraversable item in Description.Traverse())
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
            if (Optional != null)
            {
                foreach (ITraversable item in Optional.Traverse())
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
            if (SecurityDefinitions != null)
            {
                foreach (ITraversable item in SecurityDefinitions.Traverse())
                {
                    yield return item;
                }
            }
            if (Security != null)
            {
                foreach (ITraversable item in Security.Traverse())
                {
                    yield return item;
                }
            }
            if (IsComposite != null)
            {
                foreach (ITraversable item in IsComposite.Traverse())
                {
                    yield return item;
                }
            }
            if (IsEvent != null)
            {
                foreach (ITraversable item in IsEvent.Traverse())
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
            if (Metadata != null)
            {
                foreach (ITraversable item in Metadata.Traverse())
                {
                    yield return item;
                }
            }
            if (PropertyGroups != null)
            {
                foreach (ITraversable item in PropertyGroups.Traverse())
                {
                    yield return item;
                }
            }
            if (EventGroups != null)
            {
                foreach (ITraversable item in EventGroups.Traverse())
                {
                    yield return item;
                }
            }
            if (ActionGroups != null)
            {
                foreach (ITraversable item in ActionGroups.Traverse())
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
                    case DescriptionName:
                        thing.Description = ValueTracker<StringHolder>.Deserialize(ref reader, DescriptionName);
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
                    case OptionalName:
                        thing.Optional = ArrayTracker<StringHolder>.Deserialize(ref reader, OptionalName);
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
                    case SecurityDefinitionsName:
                        thing.SecurityDefinitions = MapTracker<TDAnything>.Deserialize(ref reader, SecurityDefinitionsName);
                        break;
                    case SecurityName:
                        thing.Security = ValueTracker<StringHolder>.Deserialize(ref reader, SecurityName);
                        break;
                    case IsCompositeName:
                        thing.IsComposite = ValueTracker<BoolHolder>.Deserialize(ref reader, IsCompositeName);
                        thing.IsCompositePrefixType = PrefixType.DoVocabulary;
                        break;
                    case IsCompositeLegacyName:
                        thing.IsComposite = ValueTracker<BoolHolder>.Deserialize(ref reader, IsCompositeName);
                        thing.IsCompositePrefixType = PrefixType.AioPlatform;
                        break;
                    case IsEventName:
                        thing.IsEvent = ValueTracker<BoolHolder>.Deserialize(ref reader, IsEventName);
                        thing.IsEventPrefixType = PrefixType.DoVocabulary;
                        break;
                    case IsEventLegacyName:
                        thing.IsEvent = ValueTracker<BoolHolder>.Deserialize(ref reader, IsEventName);
                        thing.IsEventPrefixType = PrefixType.AioPlatform;
                        break;
                    case TypeRefName:
                        thing.TypeRef = ValueTracker<StringHolder>.Deserialize(ref reader, TypeRefName);
                        thing.TypeRefPrefixType = PrefixType.DoVocabulary;
                        break;
                    case TypeRefLegacyName:
                        thing.TypeRef = ValueTracker<StringHolder>.Deserialize(ref reader, TypeRefName);
                        thing.TypeRefPrefixType = PrefixType.AioPlatform;
                        break;
                    case MetadataName:
                        thing.Metadata = ValueTracker<TDAnything>.Deserialize(ref reader, MetadataName);
                        break;
                    case PropertyGroupsName:
                        thing.PropertyGroups = ArrayTracker<TDAffordanceGroup>.Deserialize(ref reader, PropertyGroupsName);
                        break;
                    case EventGroupsName:
                        thing.EventGroups = ArrayTracker<TDAffordanceGroup>.Deserialize(ref reader, EventGroupsName);
                        break;
                    case ActionGroupsName:
                        thing.ActionGroups = ArrayTracker<TDAffordanceGroup>.Deserialize(ref reader, ActionGroupsName);
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
