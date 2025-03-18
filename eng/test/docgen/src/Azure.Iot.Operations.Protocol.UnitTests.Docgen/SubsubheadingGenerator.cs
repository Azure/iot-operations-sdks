namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Xml;

    public class SubsubheadingGenerator : IDocumentationGenerator
    {
        private readonly string subsubheading;

        public SubsubheadingGenerator(XmlElement subsubheadingElt, string itemName = "")
        {
            this.subsubheading = subsubheadingElt.HasChildNodes ? subsubheadingElt.InnerText : itemName;
        }

        public string Subsubheading { get => this.subsubheading; }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            markdownFile.Break();
            markdownFile.Subsubheading(this.subsubheading);
        }
    }
}
