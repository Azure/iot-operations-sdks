namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;
    using System.Xml.Schema;

    public class DocumentGenerator : IDocumentationGenerator
    {
        private readonly string protoDocFileName;
        private readonly List<IDocumentationGenerator> documentationGenerators;

        public DocumentGenerator(string protoDocPath, JsonSchemata jsonSchemata, DefaultValues defaultValues, ExampleCatalog exampleCatalog, TestCaseCatalog testCaseCatalog, CompletenessChecker? completenessChecker = null)
        {
            protoDocFileName = Path.GetFileName(protoDocPath);

            XmlReaderSettings protoDocReaderSettings = new XmlReaderSettings();
            protoDocReaderSettings.IgnoreWhitespace = false;
            protoDocReaderSettings.ValidationType = ValidationType.Schema;
            protoDocReaderSettings.ValidationFlags |= XmlSchemaValidationFlags.ProcessSchemaLocation;
            protoDocReaderSettings.ValidationEventHandler += new ValidationEventHandler((object? sender, ValidationEventArgs args) => throw new Exception($"Protodoc file {protoDocPath} failed XSD validation: {args.Message}"));

            XmlReader protoDocReader = XmlReader.Create(protoDocPath, protoDocReaderSettings);

            XmlDocument protoDoc = new XmlDocument();
            protoDoc.PreserveWhitespace = true;
            protoDoc.Load(protoDocReader);

            this.documentationGenerators = new List<IDocumentationGenerator>();

            XmlElement docElt = protoDoc.DocumentElement!;
            XmlElement headerElt = docElt["Header"]!;

            CompletenessChecker.ProcessHeader(headerElt);
            ObjectPropertyTableGenerator.ProcessHeader(headerElt);

            XmlElement bodyElt = docElt["Body"]!;

            for (int i = 0; i < bodyElt.ChildNodes.Count; i++)
            {
                XmlNode xmlNode = bodyElt.ChildNodes[i]!;
                if (xmlNode.NodeType == XmlNodeType.Element)
                {
                    XmlElement xmlElement = (XmlElement)xmlNode;
                    switch (xmlElement.Name)
                    {
                        case "Title":
                            this.documentationGenerators.Add(new TitleGenerator(xmlElement));
                            break;
                        case "Heading":
                            this.documentationGenerators.Add(new HeadingGenerator(xmlElement));
                            break;
                        case "Section":
                            this.documentationGenerators.Add(new SectionGenerator(xmlElement, jsonSchemata, defaultValues, exampleCatalog, testCaseCatalog, completenessChecker));
                            break;
                        case "Paragraph":
                            this.documentationGenerators.Add(new ParagraphGenerator(xmlElement));
                            break;
                        case "Itemization":
                            this.documentationGenerators.Add(new ItemizationGenerator(xmlElement));
                            break;
                    }
                }
            }
        }

        public void ProduceDocument(string docFilePath)
        {
            MarkdownFile markdownFile = new MarkdownFile(docFilePath);
            markdownFile.Comment($"Auto-generated from file {protoDocFileName} -- DO NOT MODIFY");
            GenerateDocumentation(markdownFile);
            markdownFile.Close();
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
