namespace Azure.Iot.Operations.Protocol.Docgen
{
    using System.Collections.Generic;
    using System.IO;
    using YamlDotNet.RepresentationModel;

    public class TestCaseCatalog
    {
        private Dictionary<string, List<TestCaseDescription>> testCaseDescriptions;

        public TestCaseCatalog(string testCaseRoot)
        {
            testCaseDescriptions = new();

            foreach (string suitePath in Directory.GetDirectories(testCaseRoot))
            {
                string suiteName = Path.GetFileName(suitePath);
                testCaseDescriptions[suiteName] = new();

                foreach (string filePath in Directory.GetFiles(suitePath, @"*.yaml"))
                {
                    string fileText = File.ReadAllText(filePath);
                    StringReader stringReader = new StringReader(fileText);
                    YamlStream yamlStream = new YamlStream();
                    yamlStream.Load(stringReader);

                    YamlMappingNode rootMap = (YamlMappingNode)yamlStream.Documents[0].RootNode;
                    YamlMappingNode descriptionMap = (YamlMappingNode)rootMap.Children["description"];

                    YamlScalarNode condition = (YamlScalarNode)descriptionMap["condition"];
                    YamlScalarNode expectation = (YamlScalarNode)descriptionMap["expect"];

                    testCaseDescriptions[suiteName].Add(new TestCaseDescription((string)condition!, (string)expectation!));
                }
            }
        }

        public IEnumerable<TestCaseDescription> GetDescriptions(string suiteName)
        {
            return testCaseDescriptions[suiteName];
        }
    }
}
