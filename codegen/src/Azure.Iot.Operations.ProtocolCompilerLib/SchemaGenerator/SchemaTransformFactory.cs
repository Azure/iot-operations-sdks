namespace Azure.Iot.Operations.ProtocolCompilerLib
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

        public static IEnumerable<ITemplateTransform> GetTelemetrySchemaTransforms(string payloadFormat, string projectName, CodeName genNamespace, Dtmi interfaceId, ITypeName schema, List<(string, string, DTSchemaInfo, bool, int)> nameDescSchemaRequiredIndices, CodeName? sharedPrefix, bool isSeparate, int mqttVersion)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                case PayloadFormat.Custom:
                    if (nameDescSchemaRequiredIndices.Any(ndsri => ndsri.Item3.GetType() != typeof(DTBytesInfo)))
                    {
                        throw new Exception($"PayloadFormat '{payloadFormat}' does not support any Telemetry schema other than 'bytes'");
                    }

                    if (!isSeparate && nameDescSchemaRequiredIndices.Count > 1)
                    {
                        throw new Exception($"PayloadFormat '{payloadFormat}' requires multiple Telemetries to have distinct topics by name");
                    }

                    yield break;
                case PayloadFormat.Avro:
                    yield return new TelemetryAvroSchema(projectName, genNamespace, schema, nameDescSchemaRequiredIndices, sharedPrefix, mqttVersion);
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new TelemetryJsonSchema(genNamespace, GetSchemaId(interfaceId, schema), schema, nameDescSchemaRequiredIndices, sharedPrefix, setIndex: true);
                    yield break;
                case PayloadFormat.Json:
                    yield return new TelemetryJsonSchema(genNamespace, GetSchemaId(interfaceId, schema), schema, nameDescSchemaRequiredIndices, sharedPrefix, setIndex: false);
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new TelemetryProto2(projectName, genNamespace, schema, nameDescSchemaRequiredIndices);
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new TelemetryProto3(projectName, genNamespace, schema, nameDescSchemaRequiredIndices);
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetPropertySchemaTransforms(string payloadFormat, string projectName, CodeName genNamespace, Dtmi interfaceId, ITypeName schema, List<(string, string, DTSchemaInfo, int, bool)> nameDescSchemaIndexFrags, CodeName? sharedPrefix, bool required, int mqttVersion)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                case PayloadFormat.Custom:
                    throw new Exception($"PayloadFormat '{payloadFormat}' is not usable in an Interface that contains a Property");
                case PayloadFormat.Avro:
                    yield return new PropertyAvroSchema(projectName, genNamespace, schema, nameDescSchemaIndexFrags, sharedPrefix, required, mqttVersion);
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new PropertyJsonSchema(genNamespace, GetSchemaId(interfaceId, schema), schema, nameDescSchemaIndexFrags, sharedPrefix, required, setIndex: true);
                    yield break;
                case PayloadFormat.Json:
                    yield return new PropertyJsonSchema(genNamespace, GetSchemaId(interfaceId, schema), schema, nameDescSchemaIndexFrags, sharedPrefix, required, setIndex: false);
                    yield break;
                case PayloadFormat.Proto2:
                case PayloadFormat.Proto3:
                    throw new Exception($"PayloadFormat '{payloadFormat}' is not usable in an Interface that contains a Property");
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetCommandSchemaTransforms(string payloadFormat, string projectName, CodeName genNamespace, Dtmi interfaceId, ITypeName schema, string commandName, string subType, string paramName, DTSchemaInfo paramSchema, CodeName? sharedPrefix, int mqttVersion, bool isNullable)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                case PayloadFormat.Custom:
                    if (paramSchema.GetType() != typeof(DTBytesInfo))
                    {
                        throw new Exception($"PayloadFormat '{payloadFormat}' does not support any Command {subType} schema other than 'bytes'");
                    }

                    yield break;
                case PayloadFormat.Avro:
                    yield return new CommandAvroSchema(projectName, genNamespace, schema, commandName, subType, paramName, paramSchema, sharedPrefix, mqttVersion, isNullable);
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new CommandJsonSchema(genNamespace, GetSchemaId(interfaceId, schema), schema, commandName, subType, paramName, paramSchema, sharedPrefix, isNullable, setIndex: true);
                    yield break;
                case PayloadFormat.Json:
                    yield return new CommandJsonSchema(genNamespace, GetSchemaId(interfaceId, schema), schema, commandName, subType, paramName, paramSchema, sharedPrefix, isNullable, setIndex: false);
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new CommandProto2(projectName, genNamespace, schema, paramName, paramSchema, isNullable);
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new CommandProto3(projectName, genNamespace, schema, paramName, paramSchema, isNullable);
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetSinglePropertyResponseSchemaTransforms(ITypeName schema, CodeName errorSchema, string payloadFormat, CodeName genNamespace, bool includeValue)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                case PayloadFormat.Custom:
                    throw new Exception($"PayloadFormat '{payloadFormat}' is not usable in an Interface that contains a Property");
                case PayloadFormat.Avro:
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new AggregateResponseJsonSchema(genNamespace, schema, errorSchema, includeValue, setIndex: true);
                    yield break;
                case PayloadFormat.Json:
                    yield return new AggregateResponseJsonSchema(genNamespace, schema, errorSchema, includeValue, setIndex: false);
                    yield break;
                case PayloadFormat.Proto2:
                case PayloadFormat.Proto3:
                    throw new Exception($"PayloadFormat '{payloadFormat}' is not usable in an Interface that contains a Property");
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetObjectSchemaTransforms(string payloadFormat, string projectName, CodeName genNamespace, Dtmi interfaceId, Dtmi objectId, string description, CodeName schema, List<(string, string, DTSchemaInfo, bool, bool, int)> nameDescSchemaIndirectRequiredIndices, CodeName? sharedPrefix, int mqttVersion, bool isResult)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    yield break;
                case PayloadFormat.Custom:
                    yield break;
                case PayloadFormat.Avro:
                    if (isResult)
                    {
                        yield return new ResultAvroSchema(projectName, genNamespace, schema, nameDescSchemaIndirectRequiredIndices, sharedPrefix, mqttVersion);
                    }
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new ObjectJsonSchema(CommonSchemaSupport.GetNamespace(objectId, sharedPrefix, genNamespace)!, GetSchemaId(objectId, schema), description, schema, nameDescSchemaIndirectRequiredIndices, sharedPrefix, setIndex: true);
                    yield break;
                case PayloadFormat.Json:
                    yield return new ObjectJsonSchema(CommonSchemaSupport.GetNamespace(objectId, sharedPrefix, genNamespace)!, GetSchemaId(objectId, schema), description, schema, nameDescSchemaIndirectRequiredIndices, sharedPrefix, setIndex: false);
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new ObjectProto2(projectName, genNamespace, schema, nameDescSchemaIndirectRequiredIndices);
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new ObjectProto3(projectName, genNamespace, schema, nameDescSchemaIndirectRequiredIndices);
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetEnumSchemaTransforms(string payloadFormat, string projectName, CodeName genNamespace, Dtmi enumId, string description, CodeName schema, string valueSchema, List<(string, string, int)> nameValueIndices, CodeName? sharedPrefix)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    yield break;
                case PayloadFormat.Custom:
                    yield break;
                case PayloadFormat.Avro:
                    yield break;
                case PayloadFormat.Cbor:
                    yield return new EnumJsonSchema(CommonSchemaSupport.GetNamespace(enumId, sharedPrefix, genNamespace)!, GetSchemaId(enumId, schema), description, schema, valueSchema, nameValueIndices);
                    yield break;
                case PayloadFormat.Json:
                    yield return new EnumJsonSchema(CommonSchemaSupport.GetNamespace(enumId, sharedPrefix, genNamespace)!, GetSchemaId(enumId, schema), description, schema, valueSchema, nameValueIndices);
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new EnumProto2(projectName, genNamespace, schema, valueSchema, nameValueIndices);
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new EnumProto3(projectName, genNamespace, schema, valueSchema, nameValueIndices);
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetArraySchemaTransforms(string payloadFormat, string projectName, CodeName genNamespace, Dtmi interfaceId, DTSchemaInfo elementSchema, string description, CodeName schema)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    yield break;
                case PayloadFormat.Custom:
                    yield break;
                case PayloadFormat.Avro:
                    yield break;
                case PayloadFormat.Cbor:
                    yield break;
                case PayloadFormat.Json:
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new ArrayProto2(projectName, genNamespace, schema, elementSchema);
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new ArrayProto3(projectName, genNamespace, schema, elementSchema);
                    yield break;
                default:
                    throw GetFormatNotRecognizedException(payloadFormat);
            }
        }

        public static IEnumerable<ITemplateTransform> GetMapSchemaTransforms(string payloadFormat, string projectName, CodeName genNamespace, Dtmi interfaceId, DTSchemaInfo mapValueSchema, string description, CodeName schema)
        {
            switch (payloadFormat)
            {
                case PayloadFormat.Raw:
                    yield break;
                case PayloadFormat.Custom:
                    yield break;
                case PayloadFormat.Avro:
                    yield break;
                case PayloadFormat.Cbor:
                    yield break;
                case PayloadFormat.Json:
                    yield break;
                case PayloadFormat.Proto2:
                    yield return new MapProto2(projectName, genNamespace, schema, mapValueSchema);
                    yield break;
                case PayloadFormat.Proto3:
                    yield return new MapProto3(projectName, genNamespace, schema, mapValueSchema);
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
                PayloadFormat.Custom => "txt",
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

        private static string GetSchemaId(Dtmi dtmi, ITypeName schema)
        {
            return $"{dtmi.AbsoluteUri.Replace(";", "_").Replace(":", "_").Replace(".", "_")}_{schema.GetTypeName(TargetLanguage.Independent)}";
        }

        private static Exception GetFormatNotRecognizedException(string payloadFormat)
        {
            return new Exception($"{DtdlMqttExtensionValues.GetStandardTerm(DtdlMqttExtensionValues.PayloadFormatPropertyFormat)} '{payloadFormat}' not recognized; must be {PayloadFormat.Itemize(" or ", "'")}");
        }
    }
}
