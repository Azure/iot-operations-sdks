namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.Xml;

    public class SectionGenerator : IDocumentationGenerator
    {
        private readonly string suiteName;

        private readonly List<IDocumentationGenerator> documentationGenerators;

        public SectionGenerator(XmlElement sectionElt, JsonSchemata jsonSchemata, DefaultValues defaultValues, ExampleCatalog exampleCatalog, TestCaseCatalog testCaseCatalog, CompletenessChecker? completenessChecker = null)
        {
            this.suiteName = sectionElt.GetAttribute("suite");

            this.documentationGenerators = new List<IDocumentationGenerator>();

            for (int i = 0; i < sectionElt.ChildNodes.Count; i++)
            {
                XmlNode xmlNode = sectionElt.ChildNodes[i]!;
                if (xmlNode.NodeType == XmlNodeType.Element)
                {
                    XmlElement xmlElement = (XmlElement)xmlNode;
                    switch (xmlNode.Name)
                    {
                        case "Heading":
                            this.documentationGenerators.Add(new HeadingGenerator(xmlElement, this.suiteName));
                            break;
                        case "Subheading":
                            this.documentationGenerators.Add(new SubheadingGenerator(xmlElement));
                            break;
                        case "Subsubheading":
                            this.documentationGenerators.Add(new SubsubheadingGenerator(xmlElement));
                            break;
                        case "Subsection":
                            this.documentationGenerators.Add(new SubsectionGenerator(xmlElement, jsonSchemata, defaultValues, exampleCatalog, testCaseCatalog, this.suiteName, completenessChecker));
                            break;
                        case "Paragraph":
                            this.documentationGenerators.Add(new ParagraphGenerator(xmlElement));
                            break;
                        case "Itemization":
                            this.documentationGenerators.Add(new ItemizationGenerator(xmlElement));
                            break;
                        case "ObjectPropertyTable":
                            this.documentationGenerators.Add(new ObjectPropertyTableGenerator(xmlElement, jsonSchemata, defaultValues, this.suiteName));
                            break;
                        case "ObjectSubtypeTable":
                            this.documentationGenerators.Add(new ObjectSubtypeTableGenerator(xmlElement, jsonSchemata));
                            break;
                        case "EnumValueTable":
                            this.documentationGenerators.Add(new EnumValueTableGenerator(xmlElement, jsonSchemata));
                            break;
                        case "TestCaseTable":
                            this.documentationGenerators.Add(new TestCaseTableGenerator(xmlElement, testCaseCatalog, this.suiteName));
                            break;
                        case "Example":
                            this.documentationGenerators.Add(new ExampleGenerator(xmlElement, exampleCatalog, this.suiteName));
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
