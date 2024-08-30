namespace Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Akri.Dtdl.Codegen;
    using Akri.Dtdl.Codegen.IntegrationTests.SchemaExtractor;
    using DTDLParser;
    using DTDLParser.Models;

    internal class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length < 4)
                {
                    Console.WriteLine("Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator annexFile modelFile patternFile testCaseFolder [casesPerTest]");
                    return 1;
                }

                string annexFile = args[0];
                string modelFile = args[1];
                string patternFile = args[2];
                string testCaseFolder = args[3];
                int casesPerTest = args.Length > 4 ? int.Parse(args[4]) : 1;

                var parsingOptions = new ParsingOptions() { AllowUndefinedExtensions = WhenToAllow.Always };
                var modelParser = new ModelParser(parsingOptions);
                string modelText = File.OpenText(modelFile).ReadToEnd();

                DtdlParseLocator parseLocator = (int parseIndex, int parseLine, out string sourceName, out int sourceLine) =>
                {
                    sourceName = modelFile;
                    sourceLine = parseLine;
                    return true;
                };

                IReadOnlyDictionary<Dtmi, DTEntityInfo> modelDict;
                try
                {
                    modelDict = modelParser.Parse(modelText, parseLocator);
                }
                catch (ParsingException pex)
                {
                    foreach (ParsingError perr in pex.Errors)
                    {
                        Console.WriteLine(perr.Message);
                    }

                    return 1;
                }

                SchemaInstantiator schemaInstantiator;
                using (JsonDocument patternDocument = JsonDocument.Parse(File.OpenText(patternFile).ReadToEnd()))
                {
                    schemaInstantiator = new SchemaInstantiator(patternDocument.RootElement);
                }

                using (JsonDocument annexDocument = JsonDocument.Parse(File.OpenText(annexFile).ReadToEnd()))
                {
                    string modelId = annexDocument.RootElement.GetProperty(AnnexFileProperties.ModelId).GetString()!;
                    Dtmi interfaceId = new Dtmi(modelId);
                    DTInterfaceInfo dtInterface = (DTInterfaceInfo)modelDict[interfaceId];

                    foreach (JsonElement elt in annexDocument.RootElement.GetProperty(AnnexFileProperties.TelemetryList).EnumerateArray())
                    {
                        string? telemName = elt.TryGetProperty(AnnexFileProperties.TelemName, out JsonElement telemNameElt) ? telemNameElt.GetString() : null;
                        SchemaTypeInfo telemTypeInfo = SchemaExtractor.GetTelemTypeInfo(dtInterface, elt);

                        if (telemName != null)
                        {
                            KeyValuePair<string, SchemaTypeInfo> fieldSchema = ((ObjectTypeInfo)telemTypeInfo).FieldSchemas.First();
                            TestTelemetry(testCaseFolder, casesPerTest, modelId, $"Test{NameFormatter.Capitalize(telemName)}", telemName, fieldSchema.Key, fieldSchema.Value, schemaInstantiator);
                        }
                        else
                        {
                            foreach (KeyValuePair<string, SchemaTypeInfo> fieldSchema in ((ObjectTypeInfo)telemTypeInfo).FieldSchemas)
                            {
                                TestTelemetry(testCaseFolder, casesPerTest, modelId, $"Test{NameFormatter.Capitalize(fieldSchema.Key)}", null, fieldSchema.Key, fieldSchema.Value, schemaInstantiator);
                            }
                        }
                    }

                    foreach (JsonElement elt in annexDocument.RootElement.GetProperty(AnnexFileProperties.CommandList).EnumerateArray())
                    {
                        string commandName = elt.GetProperty(AnnexFileProperties.CommandName).GetString()!;
                        SchemaTypeInfo? requestTypeInfo = SchemaExtractor.GetCmdReqTypeInfo(dtInterface, elt);
                        SchemaTypeInfo? responseTypeInfo = SchemaExtractor.GetCmdRespTypeInfo(dtInterface, elt);

                        TestCommand(testCaseFolder, casesPerTest, modelId, $"Test{NameFormatter.Capitalize(commandName)}", commandName, requestTypeInfo, responseTypeInfo, schemaInstantiator);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Akri.Dtdl.Codegen.IntegrationTests.TestCaseGenerator failed with exception: {ex.Message}");
                return 1;
            }

            return 0;
        }

        static void TestTelemetry(string testCaseFolder, int casesPerTest, string modelId, string testName, string? telemName, string fieldName, SchemaTypeInfo fieldSchema, SchemaInstantiator schemaInstantiator)
        {
            using (var fileStream = new FileStream(Path.Combine(testCaseFolder, $"{testName}.json"), FileMode.Create, FileAccess.Write))
            {
                JsonWriterOptions jsonWriterOptions = new JsonWriterOptions { Indented = true };
                using (Utf8JsonWriter jsonWriter = new Utf8JsonWriter(fileStream, jsonWriterOptions))
                {
                    jsonWriter.WriteStartArray();

                    for (int testCase = 0; testCase < casesPerTest; testCase++)
                    {
                        jsonWriter.WriteStartObject();

                        jsonWriter.WriteString("model", modelId);
                        jsonWriter.WriteString("pattern", "telemetry");

                        if (telemName != null)
                        {
                            jsonWriter.WriteString("name", telemName);
                        }

                        jsonWriter.WriteStartObject("value");
                        jsonWriter.WritePropertyName(fieldName);
                        schemaInstantiator.InstantiateSchema(jsonWriter, fieldSchema);
                        jsonWriter.WriteEndObject();

                        jsonWriter.WriteEndObject();
                    }

                    jsonWriter.WriteEndArray();

                    jsonWriter.Flush();
                }
            }
        }

        static void TestCommand(string testCaseFolder, int casesPerTest, string modelId, string testName, string? commandName, SchemaTypeInfo? requestSchema, SchemaTypeInfo? responseSchema, SchemaInstantiator schemaInstantiator)
        {
            using (var fileStream = new FileStream(Path.Combine(testCaseFolder, $"{testName}.json"), FileMode.Create, FileAccess.Write))
            {
                JsonWriterOptions jsonWriterOptions = new JsonWriterOptions { Indented = true };
                using (Utf8JsonWriter jsonWriter = new Utf8JsonWriter(fileStream, jsonWriterOptions))
                {
                    jsonWriter.WriteStartArray();

                    for (int testCase = 0; testCase < casesPerTest; testCase++)
                    {
                        jsonWriter.WriteStartObject();

                        jsonWriter.WriteString("model", modelId);
                        jsonWriter.WriteString("pattern", "command");
                        jsonWriter.WriteString("name", commandName);

                        jsonWriter.WritePropertyName("request");
                        if (requestSchema != null)
                        {
                            schemaInstantiator.InstantiateSchema(jsonWriter, requestSchema);
                        }
                        else
                        {
                            jsonWriter.WriteNullValue();
                        }

                        jsonWriter.WritePropertyName("response");
                        if (responseSchema != null)
                        {
                            schemaInstantiator.InstantiateSchema(jsonWriter, responseSchema);
                        }
                        else
                        {
                            jsonWriter.WriteNullValue();
                        }

                        InstantiateMetadata(jsonWriter, "requestMeta", schemaInstantiator);
                        InstantiateMetadata(jsonWriter, "responseMeta", schemaInstantiator);

                        jsonWriter.WriteEndObject();
                    }

                    jsonWriter.WriteEndArray();

                    jsonWriter.Flush();
                }
            }
        }

        static void InstantiateMetadata(Utf8JsonWriter jsonWriter, string propertyName, SchemaInstantiator schemaInstantiator)
        {
            SchemaTypeInfo metadataSchema = new PrimitiveTypeInfo("String");

            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteStartObject();

            jsonWriter.WritePropertyName("val1");
            schemaInstantiator.InstantiateSchema(jsonWriter, metadataSchema);

            jsonWriter.WritePropertyName("val2");
            schemaInstantiator.InstantiateSchema(jsonWriter, metadataSchema);

            jsonWriter.WriteEndObject();
        }
    }
}
