// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.TDParser
{
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using Azure.Iot.Operations.TDParser.Model;

    public class TDParser
    {
        public static List<TDThing> Parse(string tdJson)
        {
            return Parse(Encoding.UTF8.GetBytes(tdJson));
        }

        public static List<TDThing> Parse(byte[] tdJson)
        {
            Utf8JsonReader reader = new Utf8JsonReader(tdJson);

            reader.Read();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                List<TDThing> things = new();

                reader.Read();
                while (reader.TokenType != JsonTokenType.EndArray)
                {
                    things.Add(TDThing.Deserialize(ref reader));
                    reader.Read();
                }

                return things;
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                TDThing? thing = TDThing.Deserialize(ref reader);
                if (thing != null)
                {
                    return new List<TDThing> { thing };
                }
            }

            return new();
        }
    }
}
