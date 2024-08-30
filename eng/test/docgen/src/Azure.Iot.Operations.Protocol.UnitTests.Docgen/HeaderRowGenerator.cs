namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;

    public class HeaderRowGenerator
    {
        public void GenerateRows(MarkdownFile markdownFile, IEnumerable<string> columnNames)
        {
            markdownFile.BeginTableRow();

            foreach (string columnName in columnNames)
            {
                markdownFile.TableCell(columnName);
            }

            markdownFile.EndTableRow();
            markdownFile.BeginTableRow();

            foreach (string columnName in columnNames)
            {
                markdownFile.TableSeparator();
            }

            markdownFile.EndTableRow();
        }
    }
}
