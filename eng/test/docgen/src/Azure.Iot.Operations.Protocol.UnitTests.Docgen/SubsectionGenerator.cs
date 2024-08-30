namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Xml;

    public class SubsectionGenerator : IDocumentationGenerator
    {
        private readonly string itemName;

        private readonly List<IDocumentationGenerator> documentationGenerators;

        public SubsectionGenerator(XmlElement subsectionElt, JsonSchemata jsonSchemata, DefaultValues defaultValues, ExampleCatalog exampleCatalog, TestCaseCatalog testCaseCatalog, string suiteName, CompletenessChecker? completenessChecker = null)
        {
            this.itemName = subsectionElt.GetAttribute("item");
            completenessChecker?.Tally(this.itemName);

            this.documentationGenerators = new List<IDocumentationGenerator>();

            for (int i = 0; i < subsectionElt.ChildNodes.Count; i++)
            {
                XmlNode xmlNode = subsectionElt.ChildNodes[i]!;
                if (xmlNode.NodeType == XmlNodeType.Element)
                {
                    XmlElement xmlElement = (XmlElement)xmlNode;
                    switch (xmlNode.Name)
                    {
                        case "Subsubheading":
                            this.documentationGenerators.Add(new SubsubheadingGenerator(xmlElement, this.itemName));
                            break;
                        case "Paragraph":
                            this.documentationGenerators.Add(new ParagraphGenerator(xmlElement));
                            break;
                        case "Itemization":
                            this.documentationGenerators.Add(new ItemizationGenerator(xmlElement));
                            break;
                        case "ObjectPropertyTable":
                            this.documentationGenerators.Add(new ObjectPropertyTableGenerator(xmlElement, jsonSchemata, defaultValues, suiteName, this.itemName));
                            break;
                        case "ObjectSubtypeTable":
                            this.documentationGenerators.Add(new ObjectSubtypeTableGenerator(xmlElement, jsonSchemata, this.itemName));
                            break;
                        case "EnumValueTable":
                            this.documentationGenerators.Add(new EnumValueTableGenerator(xmlElement, jsonSchemata, this.itemName));
                            break;
                        case "TestCaseTable":
                            this.documentationGenerators.Add(new TestCaseTableGenerator(xmlElement, testCaseCatalog, suiteName));
                            break;
                        case "Example":
                            this.documentationGenerators.Add(new ExampleGenerator(xmlElement, exampleCatalog, suiteName));
                            break;
                        case "CodeBlock":
                            this.documentationGenerators.Add(new CodeBlockGenerator(xmlElement));
                            break;
                    }
                }
            }
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            foreach (IDocumentationGenerator documentationGenerator in this.documentationGenerators)
            {
                documentationGenerator.GenerateDocumentation(markdownFile);
            }
        }
    }
}
