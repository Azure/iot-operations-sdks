namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Xml;

    public class ItemizationGenerator : IDocumentationGenerator
    {
        private readonly List<IDocumentationGenerator> documentationGenerators;

        public ItemizationGenerator(XmlElement itemizationElt)
        {
            this.documentationGenerators = new List<IDocumentationGenerator>();

            for (int i = 0; i < itemizationElt.ChildNodes.Count; i++)
            {
                XmlNode xmlNode = itemizationElt.ChildNodes[i]!;
                if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name == "Item")
                {
                    this.documentationGenerators.Add(new ItemGenerator((XmlElement)xmlNode, 0));
                }
                else if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name == "Indent")
                {
                    this.documentationGenerators.Add(new IndentGenerator((XmlElement)xmlNode, 1));
                }
            }
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            foreach (IDocumentationGenerator generator in this.documentationGenerators)
            {
                generator.GenerateDocumentation(markdownFile);
            }

            markdownFile.Break();
        }
    }
}
