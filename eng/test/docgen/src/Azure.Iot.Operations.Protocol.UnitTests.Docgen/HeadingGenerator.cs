namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Xml;

    public class HeadingGenerator : IDocumentationGenerator
    {
        private readonly string heading;

        public HeadingGenerator(XmlElement headingElt, string itemName = "")
        {
            this.heading = headingElt.HasChildNodes ? headingElt.InnerText : itemName;
        }

        public string Heading { get => this.heading; }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            markdownFile.Break();
            markdownFile.Heading(this.heading);
        }
    }
}
