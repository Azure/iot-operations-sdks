namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Xml;

    public class EnumValueRowGenerator
    {
        private readonly JsonSchemata jsonSchemata;
        private readonly string objectName;

        public EnumValueRowGenerator(XmlElement tableElt, JsonSchemata jsonSchemata, string itemName)
        {
            this.jsonSchemata = jsonSchemata;
            this.objectName = tableElt.HasAttribute("object") ? tableElt.GetAttribute("object") : itemName;
        }

        public void GenerateRows(MarkdownFile markdownFile, IEnumerable<TableColumnSchema> columns)
        {
            JsonElement anyOfElt = jsonSchemata.GetSchema(objectName).RootElement.GetProperty("anyOf");
            foreach (JsonElement enumValueElt in anyOfElt.EnumerateArray())
            {
                this.GenerateRow(markdownFile, columns, enumValueElt);
            }
        }

        private void GenerateRow(MarkdownFile markdownFile, IEnumerable<TableColumnSchema> columns, JsonElement enumValueElt)
        {
            markdownFile.BeginTableRow();

            foreach (TableColumnSchema column in columns)
            {
                markdownFile.TableCell(enumValueElt.GetProperty(column.Field).GetString() ?? string.Empty);
            }

            markdownFile.EndTableRow();
        }
    }
}
