namespace Akri.Dtdl.Codegen
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using DTDLParser;
    using DTDLParser.Models;

    public static class SchemaTransformFactory
    {
        private static Dictionary<string, int> uniquifiers = new();

        public static IEnumerable<ITemplateTransform> GetTelemetrySchemaTransforms(string payloadFormat, string projectName, string genNamespace, Dtmi interfaceId, string schema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    throw new Exception($"PayloadFormat '{PayloadFormat.Raw}' is not supported for Interfaces whose contents include Telemetry");
                case PayloadFormat.Avro:
                    yield return new TelemetryAvroSchema(projectName, genNamespace, schema, nameDescSchemaRequiredIndices, GetDtmiToUniqueSchemaNameDelegate(interfaceId));
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new TelemetryJsonSchema(genNamespace, interfaceId.AbsoluteUri, schema, nameDescSchemaRequiredIndices, GetDtmiToSchemaNameDelegate(interfaceId), setIndex: true);
                    yield break;
                case PayloadFormat.Json:
                    yield return new TelemetryJsonSchema(genNamespace, interfaceId.AbsoluteUri, schema, nameDescSchemaRequiredIndices, GetDtmiToSchemaNameDelegate(interfaceId), setIndex: false);
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new TelemetryProto2(projectName, genNamespace, schema, nameDescSchemaRequiredIndices, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new TelemetryProto3(projectName, genNamespace, schema, nameDescSchemaRequiredIndices, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetCommandSchemaTransforms(string payloadFormat, string projectName, string genNamespace, Dtmi interfaceId, string schema, string commandName, string subType, string paramName, DTSchemaInfo paramSchema, bool isNullable, string interfaceIdAsNamespace, string normalizedVersionSuffix)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    if (paramSchema.GetType() != typeof(DTBytesInfo))
                    {
                        throw new Exception($"PayloadFormat '{PayloadFormat.Raw}' does not support any Command {subType} schema other than 'bytes'");
                    }

                    yield break;
                case PayloadFormat.Avro:
                    yield return new CommandAvroSchema(projectName, genNamespace, schema, commandName, subType, paramName, paramSchema, isNullable, GetDtmiToUniqueSchemaNameDelegate(interfaceId));
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new CommandJsonSchema(genNamespace, interfaceId.AbsoluteUri, schema, commandName, subType, paramName, paramSchema, isNullable, GetDtmiToSchemaNameDelegate(interfaceId), setIndex: true, interfaceIdAsNamespace, normalizedVersionSuffix);
                    yield break;
                case PayloadFormat.Json:
                    yield return new CommandJsonSchema(genNamespace, interfaceId.AbsoluteUri, schema, commandName, subType, paramName, paramSchema, isNullable, GetDtmiToSchemaNameDelegate(interfaceId), setIndex: false, interfaceIdAsNamespace, normalizedVersionSuffix);
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new CommandProto2(projectName, genNamespace, schema, paramName, paramSchema, isNullable, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new CommandProto3(projectName, genNamespace, schema, paramName, paramSchema, isNullable, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetObjectSchemaTransforms(string payloadFormat, string projectName, string genNamespace, Dtmi interfaceId, Dtmi objectId, string description, string schema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    yield break;
                case PayloadFormat.Avro:
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new ObjectJsonSchema(genNamespace, objectId.AbsoluteUri, description, schema, nameDescSchemaRequiredIndices, GetDtmiToSchemaNameDelegate(interfaceId), setIndex: true);
                    yield break;
                case PayloadFormat.Json:
                    yield return new ObjectJsonSchema(genNamespace, objectId.AbsoluteUri, description, schema, nameDescSchemaRequiredIndices, GetDtmiToSchemaNameDelegate(interfaceId), setIndex: false);
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new ObjectProto2(projectName, genNamespace, schema, nameDescSchemaRequiredIndices, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new ObjectProto3(projectName, genNamespace, schema, nameDescSchemaRequiredIndices, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetEnumSchemaTransforms(string payloadFormat, string projectName, string genNamespace, Dtmi enumId, string description, string schema, Dtmi valueSchemaId, List<(string, string, int)> nameValueIndices)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    yield break;
                case PayloadFormat.Avro:
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new EnumJsonSchema(genNamespace, enumId.AbsoluteUri, description, schema, valueSchemaId, nameValueIndices);
                    yield break;
                case PayloadFormat.Json:
                    yield return new EnumJsonSchema(genNamespace, enumId.AbsoluteUri, description, schema, valueSchemaId, nameValueIndices);
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new EnumProto2(projectName, genNamespace, schema, valueSchemaId, nameValueIndices);
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new EnumProto3(projectName, genNamespace, schema, valueSchemaId, nameValueIndices);
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetArraySchemaTransforms(string payloadFormat, string projectName, string genNamespace, Dtmi interfaceId, DTSchemaInfo elementSchema, string description, string schema)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    yield break;
                case PayloadFormat.Avro:
                    yield break;
                case PayloadFormat.Cbor:
                    yield break;
                case PayloadFormat.Json:
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new ArrayProto2(projectName, genNamespace, schema, elementSchema, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new ArrayProto3(projectName, genNamespace, schema, elementSchema, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetMapSchemaTransforms(string payloadFormat, string projectName, string genNamespace, Dtmi interfaceId, DTSchemaInfo mapValueSchema, string description, string schema)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    yield break;
                case PayloadFormat.Avro:
                    yield break;
                case PayloadFormat.Cbor:
                    yield break;
                case PayloadFormat.Json:
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new MapProto2(projectName, genNamespace, schema, mapValueSchema, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new MapProto3(projectName, genNamespace, schema, mapValueSchema, GetDtmiToSchemaNameDelegate(interfaceId));
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetSchemaTransforms(string payloadFormat)
        {
            string ext = payloadFormat switch
            {
                PayloadFormat.Raw => "txt",
                PayloadFormat.Avro => "avsc",
                PayloadFormat.Cbor => "json",
                PayloadFormat.Json => "json",
                PayloadFormat.Proto2 => "proto",
                PayloadFormat.Proto3 => "proto",
                _ => throw GetFormatNotRecognizedException(payloadFormat)
            };

            foreach (string resourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                Regex rx = new($"^{Assembly.GetExecutingAssembly().GetName().Name}\\.{ResourceNames.SchemaFolder}(?:\\.(\\w+))+\\.(\\w+)\\.{ext}$");
                Match? match = rx.Match(resourceName);
                if (match.Success)
                {
                    string folderPath = Path.Combine(ResourceNames.IncludeFolder, Path.Combine(match.Groups[1].Captures.Select(c => c.Value).ToArray()));
                    string fileName = $"{match.Groups[2].Captures[0].Value}.{ext}";
                    StreamReader resourceReader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)!);

                    yield return new SchemaTransform(folderPath, fileName, resourceReader.ReadToEnd());
                }
            }
        }

        private static DtmiToSchemaName GetDtmiToSchemaNameDelegate(Dtmi interfaceId)
        {
            return (dtmi, schemaKind) => { return NameFormatter.DtmiToSchemaName(dtmi, interfaceId, schemaKind); };
        }

        private static DtmiToSchemaName GetDtmiToUniqueSchemaNameDelegate(Dtmi interfaceId)
        {
            return (dtmi, schemaKind) =>
            {
                string baseName = NameFormatter.DtmiToSchemaName(dtmi, interfaceId, schemaKind);
                if (uniquifiers.TryGetValue(baseName, out int uniquifier))
                {
                    uniquifiers[baseName] = uniquifier + 1;
                    return $"{baseName}___{uniquifier}";
                }
                else
                {
                    uniquifiers[baseName] = 1;
                    return baseName;
                }
            };
        }

        private static Exception GetFormatNotRecognizedException(string payloadFormat)
        {
            return new Exception($"{DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.PayloadFormatPropertyFormat)} '{payloadFormat}' not recognized; must be {PayloadFormat.Itemize(" or ", "'")}");
        }
    }
}
