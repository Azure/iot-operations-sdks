namespace Azure.Iot.Operations.TDParser.Model
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class TDAction : IEquatable<TDAction>, IDeserializable<TDAction>
    {
        public const string DescriptionName = "description";
        public const string InputName = "input";
        public const string OutputName = "output";
        public const string IdempotentName = "idempotent";
        public const string SafeName = "safe";
        public const string FormsName = "forms";

        public ValueTracker<StringHolder>? Description { get; set; }

        public ValueTracker<TDDataSchema>? Input { get; set; }

        public ValueTracker<TDDataSchema>? Output { get; set; }

        public ValueTracker<BoolHolder>? Idempotent { get; set; }

        public ValueTracker<BoolHolder>? Safe { get; set; }

        public ArrayTracker<TDForm>? Forms { get; set; }

        public Dictionary<string, long> PropertyNames { get; set; } = new();

        public virtual bool Equals(TDAction? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Description == other.Description &&
                       Input == other.Input &&
                       Output == other.Output &&
                       Idempotent == other.Idempotent &&
                       Safe == other.Safe &&
                       Forms == other.Forms;
            }
        }

        public override int GetHashCode()
        {
            return (Description, Input, Output, Idempotent, Safe, Forms).GetHashCode();
        }

        public static bool operator ==(TDAction? left, TDAction? right)
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

        public static bool operator !=(TDAction? left, TDAction? right)
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
            else if (obj is not TDAction other)
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
            if (Input != null)
            {
                foreach (ITraversable item in Input.Traverse())
                {
                    yield return item;
                }
            }
            if (Output != null)
            {
                foreach (ITraversable item in Output.Traverse())
                {
                    yield return item;
                }
            }
            if (Idempotent != null)
            {
                foreach (ITraversable item in Idempotent.Traverse())
                {
                    yield return item;
                }
            }
            if (Safe != null)
            {
                foreach (ITraversable item in Safe.Traverse())
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

        public static TDAction Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException($"expected JSON object but found {reader.TokenType}");
            }

            TDAction action = new();

            reader.Read();
            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString()!;
                ParsingSupport.CheckForDuplicatePropertyName(ref reader, propertyName, action.PropertyNames, "action");
                action.PropertyNames[propertyName] = reader.TokenStartIndex;
                reader.Read();

                switch (propertyName)
                {
                    case DescriptionName:
                        action.Description = ValueTracker<StringHolder>.Deserialize(ref reader);
                        break;
                    case InputName:
                        action.Input = ValueTracker<TDDataSchema>.Deserialize(ref reader);
                        break;
                    case OutputName:
                        action.Output = ValueTracker<TDDataSchema>.Deserialize(ref reader);
                        break;
                    case IdempotentName:
                        action.Idempotent = ValueTracker<BoolHolder>.Deserialize(ref reader);
                        break;
                    case SafeName:
                        action.Safe = ValueTracker<BoolHolder>.Deserialize(ref reader);
                        break;
                    case FormsName:
                        action.Forms = ArrayTracker<TDForm>.Deserialize(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                reader.Read();
            }

            return action;
        }
    }
}
