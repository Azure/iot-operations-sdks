namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class ObjectPropertyTableGenerator : IDocumentationGenerator
    {
        private static readonly HeaderRowGenerator HeaderRowGenerator = new HeaderRowGenerator();

        private static readonly List<TableColumnSchema> columns = new();

        private readonly XmlElement tableElt;
        private readonly ObjectPropertyRowGenerator propertyRowGenerator;

        public static void ProcessHeader(XmlElement headerElt)
        {
            columns.Clear();

            for (int i = 0; i < headerElt.ChildNodes.Count; i++)
            {
                XmlNode xmlNode = headerElt.ChildNodes[i]!;
                if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name == "ObjectPropertyTable")
                {
                    XmlElement tableElt = (XmlElement)xmlNode;
                    for (int j = 0; j < tableElt.ChildNodes.Count; j++)
                    {
                        XmlNode subNode = tableElt.ChildNodes[j]!;
                        if (subNode.NodeType == XmlNodeType.Element && subNode.Name == "Column")
                        {
                            columns.Add(new TableColumnSchema((XmlElement)subNode));
                        }
                    }
                }
            }
        }

        public ObjectPropertyTableGenerator(XmlElement tableElt, JsonSchemata jsonSchemata, DefaultValues defaultValues, string suiteName, string itemName = "")
        {
            this.tableElt = tableElt;

            this.propertyRowGenerator = new ObjectPropertyRowGenerator(tableElt, jsonSchemata, defaultValues, suiteName, itemName);
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            IEnumerable<TableColumnSchema> includedColumns = columns.Where(c => c.ConditionOn == string.Empty || tableElt.HasAttribute(c.ConditionOn));

            HeaderRowGenerator.GenerateRows(markdownFile, includedColumns.Select(c => c.Name));

            string? defaultPath = tableElt.HasAttribute("defaults") ? tableElt.GetAttribute("defaults") : null;
            this.propertyRowGenerator.GenerateRows(markdownFile, includedColumns, defaultPath);

            markdownFile.EndTable();
        }
    }
}
