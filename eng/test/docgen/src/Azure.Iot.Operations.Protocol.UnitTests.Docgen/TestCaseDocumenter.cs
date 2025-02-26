using YamlDotNet.RepresentationModel;

namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.IO;

    public class TestCaseDocumenter : IDocumentationGenerator
    {
        private readonly string testCaseRoot;
        private readonly string docFilePath;

        public TestCaseDocumenter(string testCaseRoot, string docFilePath)
        {
            this.testCaseRoot = testCaseRoot;
            this.docFilePath = docFilePath;
        }

        public void ProduceDocument()
        {
            MarkdownFile markdownFile = new MarkdownFile(docFilePath);
            GenerateDocumentation(markdownFile);
            markdownFile.Close();
        }

        public void GenerateDocumentation(MarkdownFile markdownFile)
        {
            markdownFile.Title("Test Cases");

            foreach (string subPath in Directory.GetDirectories(testCaseRoot))
            {
                DocumentTestCases(subPath, markdownFile);
            }
        }

        public void DocumentTestCases(string path, MarkdownFile markdownFile)
        {
            markdownFile.Break();
            markdownFile.Heading(Path.GetFileName(path));

            string[] filePaths = Directory.GetFiles(path, @"*.yaml");
            if (filePaths.Length > 0 )
            {
                markdownFile.BeginTableRow();
                markdownFile.TableCell("Normative statement");
                markdownFile.TableCell("Expected behavior");
                markdownFile.EndTableRow();

                markdownFile.BeginTableRow();
                markdownFile.TableSeparator();
                markdownFile.TableSeparator();
                markdownFile.EndTableRow();

                foreach (string filePath in filePaths)
                {
                    string fileText = File.ReadAllText(filePath);
                    StringReader stringReader = new StringReader(fileText);
                    YamlStream yamlStream = new YamlStream();
                    yamlStream.Load(stringReader);

                    YamlMappingNode rootMap = (YamlMappingNode)yamlStream.Documents[0].RootNode;
                    YamlMappingNode descriptionMap = (YamlMappingNode)rootMap.Children["description"];

                    YamlScalarNode condition = (YamlScalarNode)descriptionMap["condition"];
                    YamlScalarNode expectation = (YamlScalarNode)descriptionMap["expect"];

                    markdownFile.BeginTableRow();
                    markdownFile.TableCell((string)condition!);
                    markdownFile.TableCell((string)expectation!);
                    markdownFile.EndTableRow();
                }
            }
        }
    }
}
