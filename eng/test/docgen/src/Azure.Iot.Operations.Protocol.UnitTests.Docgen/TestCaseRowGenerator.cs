namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Xml;

    public class TestCaseRowGenerator
    {
        private readonly TestCaseCatalog testCaseCatalog;
        private readonly string suiteName;

        public TestCaseRowGenerator(XmlElement tableElt, TestCaseCatalog testCaseCatalog, string suiteName)
        {
            this.testCaseCatalog = testCaseCatalog;
            this.suiteName = tableElt.HasAttribute("suite") ? tableElt.GetAttribute("suite") : suiteName;
        }

        public void GenerateRows(MarkdownFile markdownFile, IEnumerable<TableColumnSchema> columns)
        {
            foreach (TestCaseDescription testCaseDescription in testCaseCatalog.GetDescriptions(suiteName))
            {
                this.GenerateRow(markdownFile, columns, testCaseDescription);
            }
        }

        private void GenerateRow(MarkdownFile markdownFile, IEnumerable<TableColumnSchema> columns, TestCaseDescription testCaseDescription)
        {
            markdownFile.BeginTableRow();

            foreach (TableColumnSchema column in columns)
            {
                switch (column.Field)
                {
                    case "condition":
                        markdownFile.TableCell(testCaseDescription.Condition);
                        break;
                    case "expect":
                        markdownFile.TableCell(testCaseDescription.Expect);
                        break;
                }
            }

            markdownFile.EndTableRow();
        }
    }
}
