namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests.EnvoyGeneratorTests
{
    using System.Linq;
    using System.Text.Json;
    using DTDLParser;
    using DTDLParser.Models;
    using NJsonSchema;
    using Azure.Iot.Operations.ProtocolCompiler;

    public class EnvoyTransformFactoryTests
    {
        private const string rootPath = "../../../EnvoyGeneratorTests";
        private const string modelsPath = $"{rootPath}/models";

        private static readonly Dtmi testInterfaceId = new Dtmi("dtmi:akri:DTDL:EnvoyGenerator:testInterface;1");

        [Theory]
        [InlineData("CommandWithoutTopic", false, 3, 1)]
        [InlineData("CommandWithCmdTopic", true, 3, 1)]
        [InlineData("CommandWithTelemTopic", false, 3, 1)]
        [InlineData("CommandWithBothTopics", true, 3, 1)]
        [InlineData("TelemetryWithoutTopic", false, 3, 1)]
        [InlineData("TelemetryWithCmdTopic", false, 3, 1)]
        [InlineData("TelemetryWithTelemTopic", true, 3, 1)]
        [InlineData("TelemetryWithBothTopics", true, 3, 1)]
        [InlineData("CommandWithoutTopic", false, 4, 2)]
        [InlineData("CommandWithCmdTopic", true, 4, 2)]
        [InlineData("CommandWithTelemTopic", false, 4, 2)]
        [InlineData("CommandWithBothTopics", true, 4, 2)]
        [InlineData("TelemetryWithoutTopic", false, 4, 2)]
        [InlineData("TelemetryWithCmdTopic", false, 4, 2)]
        [InlineData("TelemetryWithTelemTopic", true, 4, 2)]
        [InlineData("TelemetryWithBothTopics", true, 4, 2)]
        public void TestCheckForRelevantTopic(string modelName, bool hasRelevantTopic, int dtdlVersion, int mqttVersion)
        {
            var modelParser = new ModelParser();

            string modelText = File.OpenText($"{modelsPath}/{modelName}.json").ReadToEnd();
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = modelParser.Parse(modelText.Replace("<[DVER]>", dtdlVersion.ToString()).Replace("<[MVER]>", mqttVersion.ToString()));
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            List<string> schemaTexts = new();
            schemaGenerator.GenerateInterfaceAnnex(GetWriter(schemaTexts), mqttVersion, null);

            using (JsonDocument annexDoc = JsonDocument.Parse(schemaTexts.First()))
            {
                bool passesValidation = true;
                try
                {
                    EnvoyTransformFactory.GetTransforms("csharp", "TestProject", annexDoc, null, null, false, true, true, false, string.Empty, null, true).ToList();
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
