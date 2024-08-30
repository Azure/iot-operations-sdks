namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Xml;

    public class SubheadingGenerator : IDocumentationGenerator
    {
        private readonly string subheading;

        public SubheadingGenerator(XmlElement subheadingElt, string itemName = "")
        {
            this.subheading = subheadingElt.HasChildNodes ? subheadingElt.InnerText : itemName;
        }

        public string Subheading { get => this.subheading; }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            markdownFile.Break();
            markdownFile.Subheading(this.subheading);
        }
    }
}
