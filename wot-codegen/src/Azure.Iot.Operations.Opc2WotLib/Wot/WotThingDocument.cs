// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    public record WotThingDocument(string FileName, string Text)
    {
        private static readonly string[] OptionalMapNames = { "schemaDefinitions", "actions", "properties", "events" };

        public static WotThingDocument Create(string fileName, string text)
        {
            JsonObject document = JsonNode.Parse(text)!.AsObject();
            if (fileName.EndsWith(".TM.json", System.StringComparison.Ordinal))
            {
                RemoveForms(document);
            }

            foreach (string optionalMapName in OptionalMapNames)
            {
                if (document[optionalMapName] is JsonObject map && map.Count == 0)
                {
                    document.Remove(optionalMapName);
                }
            }

            return new WotThingDocument(
                fileName,
                document.ToJsonString(new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true,
                }));
        }

        private static void RemoveForms(JsonObject document)
        {
            document.Remove("forms");

            foreach (string affordanceMapName in new[] { "actions", "properties", "events" })
            {
                if (document[affordanceMapName] is not JsonObject affordances)
                {
                    continue;
                }

                foreach (KeyValuePair<string, JsonNode?> affordance in affordances)
                {
                    affordance.Value?.AsObject().Remove("forms");
                }
            }
        }
    }
}
