namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests.SchemaGeneratorTests
{
    using DTDLParser;
    using DTDLParser.Models;
    using NJsonSchema;
    using NJsonSchema.Validation;
    using Azure.Iot.Operations.ProtocolCompiler;

    public class SchemaGeneratorTests
    {
        private const string rootPath = "../../../SchemaGeneratorTests";
        private const string modelsPath = $"{rootPath}/models";
        private const string avroMetaSchemaPath = $"{rootPath}/metaSchemas/AVRO/avro-avsc.json";
        private const string jsonMetaSchemaPath = $"{rootPath}/metaSchemas/JSON/json-schema.json";
        private const int mqttDtdlVersionOffset = 2;

        private static readonly int[] mqttVersions = { 1, 2 };
        private static readonly string[] testedFormats = { PayloadFormat.Avro, PayloadFormat.Cbor, PayloadFormat.Json, PayloadFormat.Proto2, PayloadFormat.Proto3 };
        private static readonly Dtmi testInterfaceId = new Dtmi("dtmi:akri:DTDL:SchemaGenerator:testInterface;1");

        private static readonly List<string> fullyInferredIndexAssignments = new List<string>
        {
            " bar = 1",
            " baz = 2",
            " fat = 3",
            " fit = 4",
            " foo = 5",
            " qux = 6",
            " wow = 7",
        };

        private static readonly List<string> partlyInferredIndexAssignments = new List<string>
        {
            " baz = 10",
            " wow = 14",
            " bar = 15",
            " fat = 16",
            " fit = 17",
            " foo = 18",
            " qux = 19",
        };

        private readonly Dictionary<string, Dictionary<int, Dictionary<string, IReadOnlyDictionary<Dtmi, DTEntityInfo>>>> models;
        private readonly JsonSchema avroMetaSchema;
        private readonly JsonSchema jsonMetaSchema;

        public SchemaGeneratorTests()
        {
            var modelParser = new ModelParser();

            models = new();
            foreach (string modelPath in Directory.GetFiles(modelsPath, @"*.json"))
            {
                string modelTemplate = File.OpenText(modelPath).ReadToEnd();

                if (modelTemplate.Contains("<[FORMAT]>"))
                {
                    var versionedModels = new Dictionary<int, Dictionary<string, IReadOnlyDictionary<Dtmi, DTEntityInfo>>>();

                    foreach (int mqttVersion in mqttVersions)
                    {
                        int dtdlVersion = mqttVersion + mqttDtdlVersionOffset;
                        var formattedModels = new Dictionary<string, IReadOnlyDictionary<Dtmi, DTEntityInfo>>();

                        foreach (string format in testedFormats)
                        {
                            string formattedModelText = modelTemplate.Replace("<[DVER]>", dtdlVersion.ToString()).Replace("<[MVER]>", mqttVersion.ToString()).Replace("<[FORMAT]>", format);
                            formattedModels[format] = modelParser.Parse(formattedModelText);
                        }

                        versionedModels[mqttVersion] = formattedModels;
                    }

                    models[Path.GetFileNameWithoutExtension(modelPath)] = versionedModels;
                }
            }

            avroMetaSchema = JsonSchema.FromFileAsync(avroMetaSchemaPath).Result;
            jsonMetaSchema = JsonSchema.FromFileAsync(jsonMetaSchemaPath).Result;
        }

        [Theory]
        [InlineData("TelemetryWithArray", 1)]
        [InlineData("TelemetryWithMap", 1)]
        [InlineData("TelemetryWithObject", 1)]
        [InlineData("TelemetryWithIntEnum", 1)]
        [InlineData("TelemetryWithStringEnum", 1)]
        [InlineData("TelemetryWithArray", 2)]
        [InlineData("TelemetryWithMap", 2)]
        [InlineData("TelemetryWithObject", 2)]
        [InlineData("TelemetryWithIntEnum", 2)]
        [InlineData("TelemetryWithStringEnum", 2)]
        public void ValidateAvroTelemetrySchemas(string modelName, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][PayloadFormat.Avro];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateTelemetrySchemas(ValidateAvroSchema, mqttVersion);
        }

        [Theory]
        [InlineData("TelemetryWithArray", 1)]
        [InlineData("TelemetryWithMap", 1)]
        [InlineData("TelemetryWithObject", 1)]
        [InlineData("TelemetryWithIntEnum", 1)]
        [InlineData("TelemetryWithStringEnum", 1)]
        [InlineData("TelemetryWithArray", 2)]
        [InlineData("TelemetryWithMap", 2)]
        [InlineData("TelemetryWithObject", 2)]
        [InlineData("TelemetryWithIntEnum", 2)]
        [InlineData("TelemetryWithStringEnum", 2)]
        public void ValidateJsonTelemetrySchemas(string modelName, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][PayloadFormat.Json];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateTelemetrySchemas(ValidateJsonSchema, mqttVersion);
        }

        [Theory]
        [InlineData("CommandWithRequestArray", 1)]
        [InlineData("CommandWithRequestMap", 1)]
        [InlineData("CommandWithRequestObject", 1)]
        [InlineData("CommandWithRequestIntEnum", 1)]
        [InlineData("CommandWithRequestStringEnum", 1)]
        [InlineData("CommandWithResponseArray", 1)]
        [InlineData("CommandWithResponseMap", 1)]
        [InlineData("CommandWithResponseObject", 1)]
        [InlineData("CommandWithResponseIntEnum", 1)]
        [InlineData("CommandWithResponseStringEnum", 1)]
        [InlineData("CommandWithRequestArray", 2)]
        [InlineData("CommandWithRequestMap", 2)]
        [InlineData("CommandWithRequestObject", 2)]
        [InlineData("CommandWithRequestIntEnum", 2)]
        [InlineData("CommandWithRequestStringEnum", 2)]
        [InlineData("CommandWithResponseArray", 2)]
        [InlineData("CommandWithResponseMap", 2)]
        [InlineData("CommandWithResponseObject", 2)]
        [InlineData("CommandWithResponseIntEnum", 2)]
        [InlineData("CommandWithResponseStringEnum", 2)]
        public void ValidateAvroCommandSchemas(string modelName, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][PayloadFormat.Avro];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateCommandSchemas(ValidateAvroSchema, mqttVersion);
        }

        [Theory]
        [InlineData("CommandWithRequestArray", 1)]
        [InlineData("CommandWithRequestMap", 1)]
        [InlineData("CommandWithRequestObject", 1)]
        [InlineData("CommandWithRequestIntEnum", 1)]
        [InlineData("CommandWithRequestStringEnum", 1)]
        [InlineData("CommandWithResponseArray", 1)]
        [InlineData("CommandWithResponseMap", 1)]
        [InlineData("CommandWithResponseObject", 1)]
        [InlineData("CommandWithResponseIntEnum", 1)]
        [InlineData("CommandWithResponseStringEnum", 1)]
        [InlineData("CommandWithRequestArray", 2)]
        [InlineData("CommandWithRequestMap", 2)]
        [InlineData("CommandWithRequestObject", 2)]
        [InlineData("CommandWithRequestIntEnum", 2)]
        [InlineData("CommandWithRequestStringEnum", 2)]
        [InlineData("CommandWithResponseArray", 2)]
        [InlineData("CommandWithResponseMap", 2)]
        [InlineData("CommandWithResponseObject", 2)]
        [InlineData("CommandWithResponseIntEnum", 2)]
        [InlineData("CommandWithResponseStringEnum", 2)]
        public void ValidateJsonCommandSchemas(string modelName, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][PayloadFormat.Json];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateCommandSchemas(ValidateJsonSchema, mqttVersion);
        }

        [Theory]
        [InlineData("TelemetryWithObject", 1)]
        [InlineData("CommandWithRequestObject", 1)]
        [InlineData("CommandWithResponseObject", 1)]
        [InlineData("TelemetryWithObject", 2)]
        [InlineData("CommandWithRequestObject", 2)]
        [InlineData("CommandWithResponseObject", 2)]
        public void ValidateJsonObjects(string modelName, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][PayloadFormat.Json];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateObjects(ValidateJsonSchema, mqttVersion);
        }

        [Theory]
        [InlineData("TelemetryWithIntEnum", 1)]
        [InlineData("TelemetryWithStringEnum", 1)]
        [InlineData("CommandWithRequestIntEnum", 1)]
        [InlineData("CommandWithRequestStringEnum", 1)]
        [InlineData("CommandWithResponseIntEnum", 1)]
        [InlineData("CommandWithResponseStringEnum", 1)]
        [InlineData("TelemetryWithIntEnum", 2)]
        [InlineData("TelemetryWithStringEnum", 2)]
        [InlineData("CommandWithRequestIntEnum", 2)]
        [InlineData("CommandWithRequestStringEnum", 2)]
        [InlineData("CommandWithResponseIntEnum", 2)]
        [InlineData("CommandWithResponseStringEnum", 2)]
        public void ValidateJsonEnums(string modelName, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][PayloadFormat.Json];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateEnums(ValidateJsonSchema, mqttVersion);
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2, 1)]
        [InlineData(PayloadFormat.Proto3, 1)]
        [InlineData(PayloadFormat.Proto2, 2)]
        [InlineData(PayloadFormat.Proto3, 2)]
        public void CheckTelemetryProtoIndexingFullyInferred(string protoFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryNoIndices"][mqttVersion][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateTelemetrySchemas(CheckProtoIndexes(fullyInferredIndexAssignments), mqttVersion);
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2, 1)]
        [InlineData(PayloadFormat.Proto3, 1)]
        [InlineData(PayloadFormat.Proto2, 2)]
        [InlineData(PayloadFormat.Proto3, 2)]
        public void CheckTelemetryProtoIndexingPartlyInferred(string protoFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetrySomeIndices"][mqttVersion][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateTelemetrySchemas(CheckProtoIndexes(partlyInferredIndexAssignments), mqttVersion);
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2, 1)]
        [InlineData(PayloadFormat.Proto3, 1)]
        [InlineData(PayloadFormat.Proto2, 2)]
        [InlineData(PayloadFormat.Proto3, 2)]
        public void CheckObjectProtoIndexingFullyInferred(string protoFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryObjectNoIndices"][mqttVersion][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateObjects(CheckProtoIndexes(fullyInferredIndexAssignments), mqttVersion);
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2, 1)]
        [InlineData(PayloadFormat.Proto3, 1)]
        [InlineData(PayloadFormat.Proto2, 2)]
        [InlineData(PayloadFormat.Proto3, 2)]
        public void CheckObjectProtoIndexingPartlyInferred(string protoFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryObjectSomeIndices"][mqttVersion][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateObjects(CheckProtoIndexes(partlyInferredIndexAssignments), mqttVersion);
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2, 1)]
        [InlineData(PayloadFormat.Proto3, 1)]
        [InlineData(PayloadFormat.Proto2, 2)]
        [InlineData(PayloadFormat.Proto3, 2)]
        public void CheckStringEnumProtoIndexingFullyInferred(string protoFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryStringEnumNoIndices"][mqttVersion][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateEnums(CheckProtoIndexes(fullyInferredIndexAssignments), mqttVersion);
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2, 1)]
        [InlineData(PayloadFormat.Proto3, 1)]
        [InlineData(PayloadFormat.Proto2, 2)]
        [InlineData(PayloadFormat.Proto3, 2)]
        public void CheckStringEnumProtoIndexingPartlyInferred(string protoFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryStringEnumSomeIndices"][mqttVersion][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            schemaGenerator.GenerateEnums(CheckProtoIndexes(partlyInferredIndexAssignments), mqttVersion);
        }

        [Theory]
        [InlineData("TelemetryExtended", PayloadFormat.Cbor, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Json, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Cbor, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Json, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Cbor, 2)]
        [InlineData("TelemetryExtended", PayloadFormat.Json, 2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2, 2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Cbor, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Json, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3, 2)]
        public void TestExtendsObject(string modelName, string payloadFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][payloadFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            int callCount = 0;
            schemaGenerator.GenerateObjects((string schemaText, string fileName, string subFolder) =>
            {
                ++callCount;
            }, mqttVersion);

            Assert.Equal(1, callCount);
        }

        [Theory]
        [InlineData("TelemetryExtended", PayloadFormat.Cbor, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Json, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Cbor, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Json, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Cbor, 2)]
        [InlineData("TelemetryExtended", PayloadFormat.Json, 2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2, 2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Cbor, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Json, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3, 2)]
        public void TestExtendsEnum(string modelName, string payloadFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][payloadFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            int callCount = 0;
            schemaGenerator.GenerateEnums((string schemaText, string fileName, string subFolder) =>
            {
                ++callCount;
            }, mqttVersion);

            Assert.Equal(1, callCount);
        }

        [Theory]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2, 2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3, 2)]
        public void TestExtendsArray(string modelName, string payloadFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][payloadFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            int callCount = 0;
            schemaGenerator.GenerateArrays((string schemaText, string fileName, string subFolder) =>
            {
                ++callCount;
            });

            Assert.Equal(1, callCount);
        }

        [Theory]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2, 1)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3, 1)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2, 2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2, 2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3, 2)]
        public void TestExtendsMap(string modelName, string payloadFormat, int mqttVersion)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][mqttVersion][payloadFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface, mqttVersion, new CodeName("TestNamespace"));

            int callCount = 0;
            schemaGenerator.GenerateMaps((string schemaText, string fileName, string subFolder) =>
            {
                ++callCount;
            });

            Assert.Equal(1, callCount);
        }

        private void ValidateAvroSchema(string schemaText, string fileName, string subFolder)
        {
            ICollection<ValidationError> errors = avroMetaSchema.Validate(schemaText);
            Assert.False(errors.Any(), $"{fileName} is not valid AVRO schema; {ErrorCount(errors.Count)}");
        }

        private void ValidateJsonSchema(string schemaText, string fileName, string subFolder)
        {
            ICollection<ValidationError> errors = jsonMetaSchema.Validate(schemaText);
            Assert.False(errors.Any(), $"{fileName} is not valid JSON schema; {ErrorCount(errors.Count)}");
        }

        private static Action<string, string, string> CheckProtoIndexes(List<string> indexAssignments)
        {
            return (string schemaText, string filename, string subFolder) =>
            {
                foreach (var assignment in indexAssignments)
                {
                    Assert.Contains(assignment, schemaText);
                }
            };
        }

        private static string ErrorCount(int count) => count > 1 ? $"there are {count} validation errors" : $"there is {count} validation error";
    }
}
