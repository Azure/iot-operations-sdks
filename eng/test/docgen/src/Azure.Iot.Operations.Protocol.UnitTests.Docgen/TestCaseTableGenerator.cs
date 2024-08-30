namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    public class TestCaseTableGenerator : IDocumentationGenerator
    {
        private static readonly HeaderRowGenerator HeaderRowGenerator = new HeaderRowGenerator();

        private readonly List<TableColumnSchema> columns = new();

        private readonly TestCaseRowGenerator TestCaseRowGenerator;

        public TestCaseTableGenerator(XmlElement tableElt, TestCaseCatalog testCaseCatalog, string suiteName)
        {
            columns = new();

            this.TestCaseRowGenerator = new TestCaseRowGenerator(tableElt, testCaseCatalog, suiteName);

            for (int i = 0; i < tableElt.ChildNodes.Count; i++)
            {
                XmlNode subNode = tableElt.ChildNodes[i]!;
                if (subNode.NodeType == XmlNodeType.Element)
                {
                    XmlElement subElt = (XmlElement)subNode;
                    if (subNode.NodeType == XmlNodeType.Element && subNode.Name == "Column")
                    {
                        columns.Add(new TableColumnSchema((XmlElement)subNode));
                    }
                }
            }
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            HeaderRowGenerator.GenerateRows(markdownFile, columns.Select(c => c.Name));

            this.TestCaseRowGenerator.GenerateRows(markdownFile, columns);

            markdownFile.EndTable();
        }
    }
}
