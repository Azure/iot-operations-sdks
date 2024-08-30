namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Xml;

    public class TitleGenerator : IDocumentationGenerator
    {
        private readonly string title;

        public TitleGenerator(XmlElement titleElt)
        {
            this.title = titleElt.InnerText;
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            markdownFile.Title(this.title);
        }
    }
}
