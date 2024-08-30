namespace Akri.Dtdl.Codegen.UnitTests.SchemaGeneratorTests
{
    using DTDLParser;
    using DTDLParser.Models;
    using NJsonSchema;
    using NJsonSchema.Validation;
    using Akri.Dtdl.Codegen;

    public class SchemaGeneratorTests
    {
        private const string rootPath = "../../../SchemaGeneratorTests";
        private const string modelsPath = $"{rootPath}/models";
        private const string avroMetaSchemaPath = $"{rootPath}/metaSchemas/AVRO/avro-avsc.json";
        private const string jsonMetaSchemaPath = $"{rootPath}/metaSchemas/JSON/json-schema.json";

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

        private readonly Dictionary<string, Dictionary<string, IReadOnlyDictionary<Dtmi, DTEntityInfo>>> models;
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
                    var formattedModels = new Dictionary<string, IReadOnlyDictionary<Dtmi, DTEntityInfo>>();

                    foreach (string format in testedFormats)
                    {
                        string formattedModelText = modelTemplate.Replace("<[FORMAT]>", format);
                        formattedModels[format] = modelParser.Parse(formattedModelText);
                    }

                    models[Path.GetFileNameWithoutExtension(modelPath)] = formattedModels;
                }
            }

            avroMetaSchema = JsonSchema.FromFileAsync(avroMetaSchemaPath).Result;
            jsonMetaSchema = JsonSchema.FromFileAsync(jsonMetaSchemaPath).Result;
        }

        [Theory]
        [InlineData("TelemetryWithArray")]
        [InlineData("TelemetryWithMap")]
        [InlineData("TelemetryWithObject")]
        [InlineData("TelemetryWithIntEnum")]
        [InlineData("TelemetryWithStringEnum")]
        public void ValidateAvroTelemetrySchemas(string modelName)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][PayloadFormat.Avro];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateTelemetrySchemas(ValidateAvroSchema);
        }

        [Theory]
        [InlineData("TelemetryWithArray")]
        [InlineData("TelemetryWithMap")]
        [InlineData("TelemetryWithObject")]
        [InlineData("TelemetryWithIntEnum")]
        [InlineData("TelemetryWithStringEnum")]
        public void ValidateJsonTelemetrySchemas(string modelName)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][PayloadFormat.Json];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateTelemetrySchemas(ValidateJsonSchema);
        }

        [Theory]
        [InlineData("CommandWithRequestArray")]
        [InlineData("CommandWithRequestMap")]
        [InlineData("CommandWithRequestObject")]
        [InlineData("CommandWithRequestIntEnum")]
        [InlineData("CommandWithRequestStringEnum")]
        [InlineData("CommandWithResponseArray")]
        [InlineData("CommandWithResponseMap")]
        [InlineData("CommandWithResponseObject")]
        [InlineData("CommandWithResponseIntEnum")]
        [InlineData("CommandWithResponseStringEnum")]
        public void ValidateAvroCommandSchemas(string modelName)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][PayloadFormat.Avro];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateCommandSchemas(ValidateAvroSchema);
        }

        [Theory]
        [InlineData("CommandWithRequestArray")]
        [InlineData("CommandWithRequestMap")]
        [InlineData("CommandWithRequestObject")]
        [InlineData("CommandWithRequestIntEnum")]
        [InlineData("CommandWithRequestStringEnum")]
        [InlineData("CommandWithResponseArray")]
        [InlineData("CommandWithResponseMap")]
        [InlineData("CommandWithResponseObject")]
        [InlineData("CommandWithResponseIntEnum")]
        [InlineData("CommandWithResponseStringEnum")]
        public void ValidateJsonCommandSchemas(string modelName)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][PayloadFormat.Json];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateCommandSchemas(ValidateJsonSchema);
        }

        [Theory]
        [InlineData("TelemetryWithObject")]
        [InlineData("CommandWithRequestObject")]
        [InlineData("CommandWithResponseObject")]
        public void ValidateJsonObjects(string modelName)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][PayloadFormat.Json];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateObjects(ValidateJsonSchema);
        }

        [Theory]
        [InlineData("TelemetryWithIntEnum")]
        [InlineData("TelemetryWithStringEnum")]
        [InlineData("CommandWithRequestIntEnum")]
        [InlineData("CommandWithRequestStringEnum")]
        [InlineData("CommandWithResponseIntEnum")]
        [InlineData("CommandWithResponseStringEnum")]
        public void ValidateJsonEnums(string modelName)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][PayloadFormat.Json];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateEnums(ValidateJsonSchema);
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2)]
        [InlineData(PayloadFormat.Proto3)]
        public void CheckTelemetryProtoIndexingFullyInferred(string protoFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryNoIndices"][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateTelemetrySchemas(CheckProtoIndexes(fullyInferredIndexAssignments));
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2)]
        [InlineData(PayloadFormat.Proto3)]
        public void CheckTelemetryProtoIndexingPartlyInferred(string protoFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetrySomeIndices"][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateTelemetrySchemas(CheckProtoIndexes(partlyInferredIndexAssignments));
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2)]
        [InlineData(PayloadFormat.Proto3)]
        public void CheckObjectProtoIndexingFullyInferred(string protoFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryObjectNoIndices"][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateObjects(CheckProtoIndexes(fullyInferredIndexAssignments));
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2)]
        [InlineData(PayloadFormat.Proto3)]
        public void CheckObjectProtoIndexingPartlyInferred(string protoFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryObjectSomeIndices"][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateObjects(CheckProtoIndexes(partlyInferredIndexAssignments));
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2)]
        [InlineData(PayloadFormat.Proto3)]
        public void CheckStringEnumProtoIndexingFullyInferred(string protoFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryStringEnumNoIndices"][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateEnums(CheckProtoIndexes(fullyInferredIndexAssignments));
        }

        [Theory]
        [InlineData(PayloadFormat.Proto2)]
        [InlineData(PayloadFormat.Proto3)]
        public void CheckStringEnumProtoIndexingPartlyInferred(string protoFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models["TelemetryStringEnumSomeIndices"][protoFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            schemaGenerator.GenerateEnums(CheckProtoIndexes(partlyInferredIndexAssignments));
        }

        [Theory]
        [InlineData("TelemetryExtended", PayloadFormat.Cbor)]
        [InlineData("TelemetryExtended", PayloadFormat.Json)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3)]
        [InlineData("CommandsExtended", PayloadFormat.Cbor)]
        [InlineData("CommandsExtended", PayloadFormat.Json)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3)]
        public void TestExtendsObject(string modelName, string payloadFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][payloadFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            int callCount = 0;
            schemaGenerator.GenerateObjects((string schemaText, string fileName, string subFolder) =>
            {
                ++callCount;
            });

            Assert.Equal(1, callCount);
        }

        [Theory]
        [InlineData("TelemetryExtended", PayloadFormat.Cbor)]
        [InlineData("TelemetryExtended", PayloadFormat.Json)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3)]
        [InlineData("CommandsExtended", PayloadFormat.Cbor)]
        [InlineData("CommandsExtended", PayloadFormat.Json)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3)]
        public void TestExtendsEnum(string modelName, string payloadFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][payloadFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            int callCount = 0;
            schemaGenerator.GenerateEnums((string schemaText, string fileName, string subFolder) =>
            {
                ++callCount;
            });

            Assert.Equal(1, callCount);
        }

        [Theory]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3)]
        public void TestExtendsArray(string modelName, string payloadFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][payloadFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

            int callCount = 0;
            schemaGenerator.GenerateArrays((string schemaText, string fileName, string subFolder) =>
            {
                ++callCount;
            });

            Assert.Equal(1, callCount);
        }

        [Theory]
        [InlineData("TelemetryExtended", PayloadFormat.Proto2)]
        [InlineData("TelemetryExtended", PayloadFormat.Proto3)]
        [InlineData("CommandsExtended", PayloadFormat.Proto2)]
        [InlineData("CommandsExtended", PayloadFormat.Proto3)]
        public void TestExtendsMap(string modelName, string payloadFormat)
        {
            IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict = models[modelName][payloadFormat];
            DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[testInterfaceId];

            var schemaGenerator = new SchemaGenerator(modelDict, "TestProject", dtInterface);

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
