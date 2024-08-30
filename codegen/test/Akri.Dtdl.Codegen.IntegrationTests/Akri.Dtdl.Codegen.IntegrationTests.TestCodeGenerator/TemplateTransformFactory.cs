namespace Akri.Dtdl.Codegen.IntegrationTests.TestCodeGenerator
{
    using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;
    using Akri.Dtdl.Codegen.IntegrationTests.T4;
    using Akri.Dtdl.Codegen;
    using System;
    using System.Collections.Generic;

    public static class TemplateTransformFactory
    {
        private static readonly Dictionary<string, FormatValues> formatValues = new()
        {
            { PayloadFormat.Avro, new FormatValues("AVRO", new AvroTranscoderFactory(), dtdlTypegen: false) },
            { PayloadFormat.Cbor, new FormatValues("CBOR", new JsonTranscoderFactory("EmptyCbor"), dtdlTypegen: true) },
            { PayloadFormat.Json, new FormatValues("JSON", new JsonTranscoderFactory("EmptyJson"), dtdlTypegen: true) },
            { PayloadFormat.Proto2, new FormatValues("protobuf", new ProtobufTranscoderFactory(), dtdlTypegen: false, additionalUsings: new List<string> { "Google.Protobuf.Reflection", "Google.Protobuf.WellKnownTypes" }) },
            { PayloadFormat.Proto3, new FormatValues("protobuf", new ProtobufTranscoderFactory(), dtdlTypegen: false, additionalUsings: new List<string> { "Google.Protobuf.Reflection", "Google.Protobuf.WellKnownTypes" }) },
        };

        public static IEnumerable<ITemplateTransform> GetTransforms(string language, string genFormat, string modelId, string genNamespace, string serviceName, string testName, List<string> testCaseNames, List<(string?, SchemaTypeInfo)> telemNameSchemas, List<(string, SchemaTypeInfo?, SchemaTypeInfo?)> cmdNameReqResps, bool doesCommandTargetExecutor, List<EnumTypeInfo> enumSchemas)
        {
            string projectComponentName = formatValues[genFormat].ProjectComponentName;
            ITranscoderFactory transcoderFactory = formatValues[genFormat].TranscoderFactory;
            bool dtdlTypegen = formatValues[genFormat].DtdlTypegen;
            List<string> additionalUsings = formatValues[genFormat].AdditionalUsings;

            switch (language)
            {
                case "csharp":
                    yield return new DotNetLibraryCsProj(testName, projectComponentName);
                    yield return new DotNetStandaloneCsProj(testName);
                    yield return new DotNetStandaloneTest(modelId, serviceName, testName, testCaseNames);
                    yield return new DotNetClientShim(genNamespace, serviceName, testName, projectComponentName, telemNameSchemas, cmdNameReqResps, transcoderFactory.GetDotnetTranscoder(), additionalUsings, doesCommandTargetExecutor);
                    yield return new DotNetServiceShim(genNamespace, serviceName, testName, projectComponentName, telemNameSchemas, cmdNameReqResps, transcoderFactory.GetDotnetTranscoder(), additionalUsings);

                    if (dtdlTypegen)
                    {
                        foreach (EnumTypeInfo enumSchema in enumSchemas)
                        {
                            yield return new DotNetEnumTokenizer(genNamespace, serviceName, testName, enumSchema);
                        }
                    }

                    break;
                case "java":
                    break;
                case "python":
                    break;
                default:
                    throw new NotSupportedException($"language '{language}' not recognized.");
            }
        }

        private readonly struct FormatValues
        {
            public FormatValues(string projectComponentName, ITranscoderFactory transcoderFactory, bool dtdlTypegen, List<string>? additionalUsings = null)
            {
                ProjectComponentName = projectComponentName;
                TranscoderFactory = transcoderFactory;
                DtdlTypegen = dtdlTypegen;
                AdditionalUsings = additionalUsings ?? new List<string>();
            }

            public readonly string ProjectComponentName;
            public readonly ITranscoderFactory TranscoderFactory;
            public readonly bool DtdlTypegen;
            public readonly List<string> AdditionalUsings;
        }
    }
}
