namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests.ThingGeneratorTests
{
    using DTDLParser;
    using DTDLParser.Models;
    using NJsonSchema;
    using NJsonSchema.Validation;
    using Azure.Iot.Operations.ProtocolCompilerLib;

    public class ThingGeneratorTests
    {
        private const string modelsPath = $"../../../SchemaGeneratorTests/models";
        private const string problematicModelsPath = $"../../../ThingGeneratorTests/problematicModels";
        private const string schemaPath = $"../../../ThingGeneratorTests/schemas/td-json-schema-validation.json";
        private const int mqttDtdlVersionOffset = 2;
        private const int maxDtdlVersion = 4;

        private static readonly int[] mqttVersions = { 1, 2, 3 };
        private static readonly int[] noMqttVersion = { 0 };
        private static readonly string[] typedFormats = { PayloadFormat.Avro, PayloadFormat.Cbor, PayloadFormat.Json, PayloadFormat.Proto2, PayloadFormat.Proto3 };
        private static readonly string[] noTypedformat = { string.Empty };
        private static readonly Dtmi testInterfaceId = new Dtmi("dtmi:akri:DTDL:SchemaGenerator:testInterface;1");
        private static readonly Dtmi faultyId = new Dtmi("dtmi:akri:DTDL:ThingGenerator:recursiveObject;1");

        private readonly JsonSchema tdSchema;
        private readonly ModelParser modelParser;

        public static IEnumerable<object[]> GetTestCases()
        {
            return GetTestCasesInt(modelsPath);
        }

        public static IEnumerable<object[]> GetProblematicTestCases()
        {
            return GetTestCasesInt(problematicModelsPath);
        }

        public ThingGeneratorTests()
        {
            tdSchema = JsonSchema.FromFileAsync(schemaPath).Result;
            modelParser = new ModelParser();
        }

        [Theory]
        [MemberData(nameof(GetTestCases))]
        public void ValidateGeneratedThingDescription(string fileName, string modelText, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = modelParser.Parse(modelText);
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            ITemplateTransform interfaceThingTransform = new InterfaceThing(dtInterface, mqttVersion);

            string thingDescription = interfaceThingTransform.TransformText();

            ICollection<ValidationError> errors = tdSchema.Validate(thingDescription);
            Assert.False(errors.Any(), $"{fileName} yields an invalid Thing Description");
        }

        [Theory]
        [MemberData(nameof(GetProblematicTestCases))]
        public void ConfirmThingGenerationFailure(string _, string modelText, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = modelParser.Parse(modelText);

            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            ITemplateTransform interfaceThingTransform = new InterfaceThing(dtInterface, mqttVersion);

            RecursionException rex = Assert.Throws<RecursionException>(interfaceThingTransform.TransformText);
            Assert.Equal(faultyId, rex.SchemaName.AsDtmi);
        }

        private static IEnumerable<object[]> GetTestCasesInt(string path)
        {
            foreach (string modelPath in Directory.GetFiles(path, @"*.json"))
            {
                string modelTemplate = File.OpenText(modelPath).ReadToEnd();
                if (modelTemplate.Contains("<[ID]>"))
                {
                    continue;
                }

                foreach (int mqttVersion in modelTemplate.Contains("<[MVER]>") ? mqttVersions : noMqttVersion)
                {
                    string versionedModel = modelTemplate;
                    if (mqttVersion > 0)
                    {
                        int dtdlVersion = int.Min(mqttVersion + mqttDtdlVersionOffset, maxDtdlVersion);
                        versionedModel = modelTemplate.Replace("<[DVER]>", dtdlVersion.ToString()).Replace("<[MVER]>", mqttVersion.ToString());
                    }

                    foreach (string format in versionedModel.Contains("<[FORMAT]>") ? typedFormats : noTypedformat)
                    {
                        string formattedModel = versionedModel;
                        if (format != string.Empty)
                        {
                            formattedModel = versionedModel.Replace("<[FORMAT]>", format);
                        }

                        yield return new object[] { Path.GetFileName(modelPath), formattedModel, mqttVersion > 0 ? mqttVersion : mqttVersions.Last() };
                    }
                }
            }
        }
    }
}
