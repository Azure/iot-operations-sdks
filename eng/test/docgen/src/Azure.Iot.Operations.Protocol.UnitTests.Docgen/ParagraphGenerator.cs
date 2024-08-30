namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Xml;

    public class ParagraphGenerator : IDocumentationGenerator
    {
        private string text;

        public ParagraphGenerator(XmlElement paragraphElt)
        {
            this.text = paragraphElt.InnerText.Replace("\n", "\r\n");
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            markdownFile.Text(Regex.Replace(this.text.Trim(), @"\r\n +", "\r\n"));
        }
    }
}
