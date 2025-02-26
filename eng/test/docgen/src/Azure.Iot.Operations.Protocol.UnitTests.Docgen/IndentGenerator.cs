namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Xml;

    public class IndentGenerator : IDocumentationGenerator
    {
        private readonly List<IDocumentationGenerator> generators;

        public IndentGenerator(XmlElement indentElt, int indentationLevel)
        {
            this.generators = new List<IDocumentationGenerator>();

            for (int i = 0; i < indentElt.ChildNodes.Count; i++)
            {
                XmlNode xmlNode = indentElt.ChildNodes[i]!;
                if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name == "Item")
                {
                    this.generators.Add(new ItemGenerator((XmlElement)xmlNode, indentationLevel));
                }
                else if (xmlNode.NodeType == XmlNodeType.Element && xmlNode.Name == "Indent")
                {
                    this.generators.Add(new IndentGenerator((XmlElement)xmlNode, indentationLevel + 1));
                }
            }
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            foreach (IDocumentationGenerator generator in this.generators)
            {
                generator.GenerateDocumentation(markdownFile);
            }

            markdownFile.Break();
        }
    }
}
