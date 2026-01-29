// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    public static class EnvoyGenerator
    {
        public static List<GeneratedItem> GenerateEnvoys(
            List<ParsedThing> parsedThings,
            List<SerializationFormat> serializationFormats,
            TargetLanguage targetLanguage,
            string genNamespace,
            string projectName,
            string? sdkPath,
            List<string> typeFileNames,
            string srcSubdir,
            bool generateProject,
            bool defaultImpl)
        {
            EnvoyTransformFactory envoyFactory = new(targetLanguage, new MultiCodeName(genNamespace), projectName, srcSubdir, defaultImpl);

            Dictionary<string, IEnvoyTemplateTransform> transforms = new();
            Dictionary<string, ErrorSpec> errorSpecs = new();
            Dictionary<string, AggregateErrorSpec> aggErrorSpecs = new();
            Dictionary<string, ConstantsSpec> schemaConstants = new();
            Dictionary<SerializationFormat, HashSet<string>> formattedTypesToSerialize = serializationFormats.ToDictionary(f => f, f => new HashSet<string>());

            foreach (ParsedThing parsedThing in parsedThings)
            {
                if (parsedThing.Thing.Title == null)
                {
                    throw new System.InvalidOperationException($"Thing defined in file '{parsedThing.FileName}' is missing a root-level 'title' property.");
                }

                CodeName serviceName = new CodeName(parsedThing.Thing.Title.Value.Value);

                List<ActionSpec> actionSpecs = ActionEnvoyGenerator.GenerateActionEnvoys(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.SchemaNamer, serviceName, envoyFactory, transforms, errorSpecs, formattedTypesToSerialize, parsedThing.ForClient, parsedThing.ForServer);
                List<EventSpec> eventSpecs = EventEnvoyGenerator.GenerateEventEnvoys(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.SchemaNamer, serviceName, envoyFactory, transforms, formattedTypesToSerialize, parsedThing.ForClient, parsedThing.ForServer);
                List<PropertySpec> propSpecs = PropertyEnvoyGenerator.GeneratePropertyEnvoys(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.SchemaNamer, serviceName, envoyFactory, transforms, errorSpecs, aggErrorSpecs, formattedTypesToSerialize, parsedThing.ForClient, parsedThing.ForServer);
                GenerateServiceEnvoys(parsedThing.SchemaNamer, serviceName, actionSpecs, propSpecs, eventSpecs, envoyFactory, transforms, parsedThing.ForClient, parsedThing.ForServer);
                CollectNamedConstants(parsedThing.Thing, parsedThing.SchemaNamer, schemaConstants);
            }

            GenerateConstantEnvoys(schemaConstants, envoyFactory, transforms);
            GenerateErrorEnvoys(errorSpecs, envoyFactory, transforms);
            GenerateAggregateErrorEnvoys(aggErrorSpecs, envoyFactory, transforms);
            GenerateSerializationEnvoys(formattedTypesToSerialize, envoyFactory, transforms);

            List<GeneratedItem> generatedEnvoys = new();

            foreach (KeyValuePair<string, IEnvoyTemplateTransform> transform in transforms)
            {
                generatedEnvoys.Add(new GeneratedItem(transform.Value.TransformText(), transform.Key, transform.Value.FolderPath));
            }

            List<string> clientFilenames = transforms.Where(t => t.Value.EndpointTarget == EndpointTarget.Client).Select(t => t.Key).ToList();
            List<string> serverFilenames = transforms.Where(t => t.Value.EndpointTarget == EndpointTarget.Server).Select(t => t.Key).ToList();
            List<string> sharedFilenames = transforms.Where(t => t.Value.EndpointTarget == EndpointTarget.Shared).Select(t => t.Key).Concat(typeFileNames).ToList();
            List<string> hiddenFilenames = transforms.Where(t => t.Value.EndpointTarget == EndpointTarget.Hidden).Select(t => t.Key).ToList();

            foreach (IEnvoyTemplateTransform project in envoyFactory.GetProjectTransforms(serializationFormats, sdkPath, clientFilenames, serverFilenames, sharedFilenames, hiddenFilenames, generateProject))
            {
                generatedEnvoys.Add(new GeneratedItem(project.TransformText(), project.FileName, project.FolderPath));
            }

            foreach (IEnvoyTemplateTransform resource in envoyFactory.GetResourceTransforms(serializationFormats))
            {
                generatedEnvoys.Add(new GeneratedItem(resource.TransformText(), resource.FileName, resource.FolderPath));
            }

            return generatedEnvoys;
        }

        private static void CollectNamedConstants(TDThing tdThing, SchemaNamer schemaNamer, Dictionary<string, ConstantsSpec> schemaConstants)
        {
            IEnumerable<KeyValuePair<string, ValueTracker<TDDataSchema>>> constDefs;
            Dictionary<string, ValueTracker<ObjectHolder>> constValues;

            if (tdThing.SchemaDefinitions?.Entries != null)
            {
                constDefs = tdThing.SchemaDefinitions.Entries.Where(d => d.Value.Value.Const?.Value != null && d.Value.Value.Type?.Value.Value != TDValues.TypeObject);
                constValues = constDefs.ToDictionary(d => d.Key, d => d.Value.Value.Const!);
                AddNamedConstants(schemaNamer.ConstantsSchema, "Global constants.", constDefs, constValues, schemaNamer, schemaConstants);
            }

            foreach (KeyValuePair<string, ValueTracker<TDDataSchema>> topLevelDef in tdThing.SchemaDefinitions?.Entries ?? new())
            {
                if (topLevelDef.Value.Value.Properties?.Entries != null && topLevelDef.Value.Value.Const?.Value.ValueMap != null)
                {
                    constDefs = topLevelDef.Value.Value.Properties.Entries;
                    constValues = topLevelDef.Value.Value.Const.Value.ValueMap!.Entries!;
                    string schemaName = schemaNamer.ApplyBackupSchemaName(topLevelDef.Value.Value.Title?.Value.Value, topLevelDef.Key);
                    AddNamedConstants(schemaName, topLevelDef.Value.Value.Description?.Value.Value, constDefs!, constValues, schemaNamer, schemaConstants);
                }
            }
        }

        private static void AddNamedConstants(string schemaName, string? description, IEnumerable<KeyValuePair<string, ValueTracker<TDDataSchema>>> constDefs, Dictionary<string, ValueTracker<ObjectHolder>> constValues, SchemaNamer schemaNamer, Dictionary<string, ConstantsSpec> schemaConstants)
        {
            if (constDefs.Any())
            {
                if (!schemaConstants.TryGetValue(schemaName, out ConstantsSpec? namedConstants))
                {
                    namedConstants = new ConstantsSpec(description, new());
                    schemaConstants[schemaName] = namedConstants;
                }

                foreach (var constDef in constDefs)
                {
                    if (constDef.Value.Value.Type?.Value.Value == TDValues.TypeString || constDef.Value.Value.Type?.Value.Value == TDValues.TypeNumber || constDef.Value.Value.Type?.Value.Value == TDValues.TypeInteger || constDef.Value.Value.Type?.Value.Value == TDValues.TypeBoolean)
                    {
                        CodeName constName = new CodeName(schemaNamer.ChooseTitleOrName(constDef.Value.Value.Title?.Value.Value, constDef.Key));
                        namedConstants.Constants[constName] = new TypedConstant(constDef.Value.Value.Type.Value.Value, constValues[constDef.Key].Value.Value!, constDef.Value.Value.Description?.Value.Value);
                    }
                }
            }
        }

        private static void GenerateServiceEnvoys(
            SchemaNamer schemaNamer,
            CodeName serviceName,
            List<ActionSpec> actionSpecs,
            List<PropertySpec> propSpecs,
            List<EventSpec> eventSpecs,
            EnvoyTransformFactory envoyFactory,
            Dictionary<string, IEnvoyTemplateTransform> transforms,
            bool generateClient,
            bool generateServer)
        {
            foreach (IEnvoyTemplateTransform transform in envoyFactory.GetServiceTransforms(schemaNamer, serviceName, actionSpecs, propSpecs, eventSpecs, generateClient, generateServer))
            {
                transforms[transform.FileName] = transform;
            }
        }

        private static void GenerateConstantEnvoys(Dictionary<string, ConstantsSpec> schemaConstants, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms)
        {
            foreach (KeyValuePair<string, ConstantsSpec> schemaConstant in schemaConstants)
            {
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetConstantTransforms(new CodeName(schemaConstant.Key), schemaConstant.Value))
                {
                    transforms[transform.FileName] = transform;
                }
            }
        }

        private static void GenerateErrorEnvoys(Dictionary<string, ErrorSpec> errorSpecs, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms)
        {
            foreach (KeyValuePair<string, ErrorSpec> errorSpec in errorSpecs)
            {
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetErrorTransforms(errorSpec.Value))
                {
                    transforms[transform.FileName] = transform;
                }
            }
        }

        private static void GenerateAggregateErrorEnvoys(Dictionary<string, AggregateErrorSpec> aggErrorSpecs, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms)
        {
            foreach (KeyValuePair<string, AggregateErrorSpec> aggErrorSpec in aggErrorSpecs)
            {
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetAggregateErrorTransforms(aggErrorSpec.Value))
                {
                    transforms[transform.FileName] = transform;
                }
            }
        }

        private static void GenerateSerializationEnvoys(Dictionary<SerializationFormat, HashSet<string>> formattedTypesToSerialize, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms)
        {
            foreach (KeyValuePair<SerializationFormat, HashSet<string>> formatTypes in formattedTypesToSerialize)
            {
                foreach (string typeToSerialize in formatTypes.Value)
                {
                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetSerializationTransforms(typeToSerialize, formatTypes.Key))
                    {
                        transforms[transform.FileName] = transform;
                    }
                }
            }
        }
    }
}
