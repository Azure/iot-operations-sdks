namespace Azure.Iot.Operations.TDParser
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public class StringHolder : BaseHolder<string>, IEquatable<StringHolder>, IDeserializable<StringHolder>
    {
        public virtual bool Equals(StringHolder? other)
        {
            if (other == null)
            {
                return false;
            }
            else
            {
                return Value == other.Value;
            }
        }

        public static StringHolder Deserialize(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new InvalidOperationException($"expected JSON string but found {reader.TokenType}");
            }

            return new StringHolder { Value = reader.GetString()! };
        }

        public IEnumerable<ITraversable> Traverse()
        {
            yield break;
        }
    }
}
