namespace Akri.Dtdl.Codegen.UnitTests.EnvoyGeneratorTests
{
    using System.Linq;
    using System.Text.Json;
    using DTDLParser;
    using DTDLParser.Models;
    using NJsonSchema;
    using Akri.Dtdl.Codegen;

    public class EnvoyTransformFactoryTests
    {
        private const string rootPath = "../../../EnvoyGeneratorTests";
        private const string modelsPath = $"{rootPath}/models";

        private static readonly Dtmi testInterfaceId = new Dtmi("dtmi:akri:DTDL:EnvoyGenerator:testInterface;1");

        [Theory]
        [InlineData("CommandWithoutTopic", false)]
        [InlineData("CommandWithCmdTopic", true)]
        [InlineData("CommandWithTelemTopic", false)]
        [InlineData("CommandWithBothTopics", true)]
        [InlineData("TelemetryWithoutTopic", false)]
        [InlineData("TelemetryWithCmdTopic", false)]
        [InlineData("TelemetryWithTelemTopic", true)]
        [InlineData("TelemetryWithBothTopics", true)]
        public void TestCheckForRelevantTopic(string modelName, bool hasRelevantTopic)
        {
            var modelParser = new ModelParser();

            string modelText = File.OpenText($"{modelsPath}/{modelName}.json").ReadToEnd();
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = modelParser.Parse(modelText);
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            List<string> schemaTexts = new();
            schemaGenerator.GenerateInterfaceAnnex(GetWriter(schemaTexts));

            using (JsonDocument annexDoc = JsonDocument.Parse(schemaTexts.First()))
            {
                bool passesValidation = true;
                try
                {
                    HashSet<string> sourceFilePaths = new();
                    EnvoyTransformFactory.GetTransforms("csharp", "TestProject", annexDoc, null, null, false, sourceFilePaths).ToList();
                }
                catch
                {
                    passesValidation = false;
                }

                Assert.Equal(hasRelevantTopic, passesValidation);
            }
        }

        private static Action<string, string, string> GetWriter(List<string> schemaTexts)
        {
            return (schemaText, fileName, subFolder) =>
            {
                schemaTexts.Add(schemaText);
            };
        }
    }
}
