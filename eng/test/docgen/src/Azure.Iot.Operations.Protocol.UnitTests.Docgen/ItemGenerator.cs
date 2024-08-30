namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Text.RegularExpressions;
    using System.Xml;

    public class ItemGenerator : IDocumentationGenerator
    {
        private string text;
        private int indentationLevel;

        public ItemGenerator(XmlElement itemElt, int indentationLevel)
        {
            this.text = itemElt.InnerText.Replace("\n", "\r\n");
            this.indentationLevel = indentationLevel;
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            markdownFile.Bullet(Regex.Replace(this.text.Trim(), @"\r\n +", "\r\n"), this.indentationLevel);
        }
    }
}
