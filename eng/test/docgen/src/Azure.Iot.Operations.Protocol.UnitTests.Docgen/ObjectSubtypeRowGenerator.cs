namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Xml;

    public class ObjectSubtypeRowGenerator
    {
        private readonly JsonSchemata jsonSchemata;
        private readonly string objectName;
        private readonly string discriminator;

        public ObjectSubtypeRowGenerator(XmlElement tableElt, JsonSchemata jsonSchemata, string itemName)
        {
            this.jsonSchemata = jsonSchemata;
            this.objectName = tableElt.HasAttribute("object") ? tableElt.GetAttribute("object") : itemName;
            this.discriminator = tableElt.GetAttribute("discriminator");
        }

        public void GenerateRows(MarkdownFile markdownFile, IEnumerable<TableColumnSchema> columns)
        {
            JsonElement anyOfElt = jsonSchemata.GetSchema(objectName).RootElement.GetProperty("anyOf");
            foreach (JsonElement subTypeElt in anyOfElt.EnumerateArray())
            {
                string refFile = subTypeElt.GetProperty("$ref").GetString()!;
                string refName = refFile.Substring(0, refFile.IndexOf('.'));

                this.GenerateRow(markdownFile, columns, refName);
            }
        }

        private void GenerateRow(MarkdownFile markdownFile, IEnumerable<TableColumnSchema> columns, string subtypeName)
        {
            JsonElement subtypeElt = jsonSchemata.GetSchema(subtypeName).RootElement;
            JsonElement discriminatingElt = subtypeElt.GetProperty("properties").GetProperty(discriminator);

            markdownFile.BeginTableRow();

            foreach (TableColumnSchema column in columns)
            {
                switch (column.Field)
                {
                    case "subtype":
                        markdownFile.TableCell($"[{subtypeName}](#{MarkdownFile.ToReference(subtypeName)})");
                        break;
                    default:
                        markdownFile.TableCell(discriminatingElt.GetProperty(column.Field).GetString() ?? string.Empty);
                        break;
                }
            }

            markdownFile.EndTableRow();
        }
    }
}
