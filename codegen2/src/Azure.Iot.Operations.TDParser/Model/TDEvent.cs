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
        public const string PlaceholderName = "dtv:placeholder";
        public const string FormsName = TDCommon.FormsName;
        public const string ContainsName = TDCommon.ContainsName;
        public const string ContainedInName = TDCommon.ContainedInName;
        public const string NamespaceName = TDCommon.NamespaceName;
        public const string WithUnitName = TDCommon.WithUnitName;
        public const string HasQuantityKindName = TDCommon.HasQuantityKindName;

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
                       HasQuantityKind == other.HasQuantityKind;
            }
        }

        public override int GetHashCode()
        {
            return (Description, Data, Placeholder, Forms, Contains, ContainedIn, Namespace, WithUnit, HasQuantityKind).GetHashCode();
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
                        break;
                    case FormsName:
                        evt.Forms = ArrayTracker<TDForm>.Deserialize(ref reader, FormsName);
                        break;
                    case ContainsName:
                        evt.Contains = ArrayTracker<StringHolder>.Deserialize(ref reader, ContainsName);
                        break;
                    case ContainedInName:
                        evt.ContainedIn = ValueTracker<StringHolder>.Deserialize(ref reader, ContainedInName);
                        break;
                    case NamespaceName:
                        evt.Namespace = ValueTracker<StringHolder>.Deserialize(ref reader, NamespaceName);
                        break;
                    case WithUnitName:
                        evt.WithUnit = ValueTracker<StringHolder>.Deserialize(ref reader, WithUnitName);
                        break;
                    case HasQuantityKindName:
                        evt.HasQuantityKind = ValueTracker<StringHolder>.Deserialize(ref reader, HasQuantityKindName);
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
