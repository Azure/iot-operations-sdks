namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDProperty : TDDataSchema, IEquatable<TDProperty>, IDeserializable<TDProperty>
    {
        public const string ReadOnlyName = "readOnly";
        public const string PlaceholderName = "dtv:placeholder";
        public const string FormsName = TDCommon.FormsName;
        public const string ContainsName = TDCommon.ContainsName;
        public const string ContainedInName = TDCommon.ContainedInName;

        public ValueTracker<BoolHolder>? ReadOnly { get; set; }

        public ValueTracker<BoolHolder>? Placeholder { get; set; }

        public ArrayTracker<TDForm>? Forms { get; set; }

        public ArrayTracker<StringHolder>? Contains { get; set; }

        public ValueTracker<StringHolder>? ContainedIn { get; set; }

        public ValueTracker<StringHolder>? Namespace { get; set; }

        public virtual bool Equals(TDProperty? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return base.Equals(other) &&
                       ReadOnly == other.ReadOnly &&
                       Placeholder == other.Placeholder &&
                       Forms == other.Forms &&
                       Contains == other.Contains &&
                       ContainedIn == other.ContainedIn &&
                       Namespace == other.Namespace;
            }
        }

        public override int GetHashCode()
        {
            return (base.GetHashCode(), ReadOnly, Placeholder, Forms, Contains, ContainedIn, Namespace).GetHashCode();
        }

        public static bool operator ==(TDProperty? left, TDProperty? right)
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

        public static bool operator !=(TDProperty? left, TDProperty? right)
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
            else if (obj is not TDProperty other)
            {
                return false;
            }
            else
            {
                return Equals(other);
            }
        }

        public override IEnumerable<ITraversable> Traverse()
        {
            foreach (ITraversable baseChild in base.Traverse())
            {
                yield return baseChild;
            }

            if (ReadOnly != null)
            {
                foreach (ITraversable item in ReadOnly.Traverse())
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
        }

        public static new TDProperty Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDProperty prop = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, prop.PropertyNames, "property");
                prop.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                if (!TryLoadPropertyValues(prop, propertyName, ref reader))
                {
                    switch (propertyName)
                    {
                        case ReadOnlyName:
                            prop.ReadOnly = ValueTracker<BoolHolder>.Deserialize(ref reader, ReadOnlyName);
                            break;
                        case PlaceholderName:
                            prop.Placeholder = ValueTracker<BoolHolder>.Deserialize(ref reader, PlaceholderName);
                            break;
                        case FormsName:
                            prop.Forms = ArrayTracker<TDForm>.Deserialize(ref reader, FormsName);
                            break;
                        case ContainsName:
                            prop.Contains = ArrayTracker<StringHolder>.Deserialize(ref reader, ContainsName);
                            break;
                        case ContainedInName:
                            prop.ContainedIn = ValueTracker<StringHolder>.Deserialize(ref reader, ContainedInName);
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }

                reader.Read();
            }

            return prop;
        }
    }
}
