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
            bool generateClient,
            bool generateServer,
            bool generateProject,
            bool defaultImpl)
        {
            EnvoyTransformFactory envoyFactory = new(targetLanguage, new CodeName(genNamespace), projectName, srcSubdir, generateClient, generateServer, defaultImpl);

            Dictionary<string, IEnvoyTemplateTransform> transforms = new();
            Dictionary<string, ErrorSpec> errorSpecs = new();
            Dictionary<string, AggregateErrorSpec> aggErrorSpecs = new();
            HashSet<string> typesToSerialize = new();
            Dictionary<string, Dictionary<string, TypedConstant>> schemaConstants = new();

            foreach (ParsedThing parsedThing in parsedThings)
            {
                if (parsedThing.Thing.Title == null)
                {
                    throw new System.InvalidOperationException($"Thing defined in file '{parsedThing.FileName}' is missing a root-level 'title' property.");
                }

                CodeName serviceName = new CodeName(parsedThing.Thing.Title?.Value.Value ?? genNamespace);

                List<ActionSpec> actionSpecs = ActionEnvoyGenerator.GenerateActionEnvoys(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.SchemaNamer, serviceName, envoyFactory, transforms, errorSpecs, typesToSerialize);
                List<EventSpec> eventSpecs = EventEnvoyGenerator.GenerateEventEnvoys(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.SchemaNamer, serviceName, envoyFactory, transforms, typesToSerialize);
                List<PropertySpec> propSpecs = PropertyEnvoyGenerator.GeneratePropertyEnvoys(parsedThing.ErrorReporter, parsedThing.Thing, parsedThing.SchemaNamer, serviceName, envoyFactory, transforms, errorSpecs, aggErrorSpecs, typesToSerialize);
                GenerateServiceEnvoys(parsedThing.SchemaNamer, serviceName, actionSpecs, propSpecs, eventSpecs, envoyFactory, transforms);
                CollectNamedConstants(parsedThing.Thing, parsedThing.SchemaNamer, schemaConstants);
            }

            GenerateConstantEnvoys(schemaConstants, envoyFactory, transforms);
            GenerateErrorEnvoys(errorSpecs, envoyFactory, transforms);
            GenerateAggregateErrorEnvoys(aggErrorSpecs, envoyFactory, transforms);
            GenerateSerializationEnvoys(typesToSerialize, envoyFactory, transforms);

            List<GeneratedItem> generatedEnvoys = new();

            foreach (KeyValuePair<string, IEnvoyTemplateTransform> transform in transforms)
            {
                generatedEnvoys.Add(new GeneratedItem(transform.Value.TransformText(), transform.Key, transform.Value.FolderPath));
            }

            foreach (IEnvoyTemplateTransform project in envoyFactory.GetProjectTransforms(serializationFormats, sdkPath, transforms.Keys.Concat(typeFileNames).ToList(), generateProject))
            {
                generatedEnvoys.Add(new GeneratedItem(project.TransformText(), project.FileName, project.FolderPath));
            }

            foreach (IEnvoyTemplateTransform resource in envoyFactory.GetResourceTransforms(serializationFormats))
            {
                generatedEnvoys.Add(new GeneratedItem(resource.TransformText(), resource.FileName, resource.FolderPath));
            }

            return generatedEnvoys;
        }

        private static void CollectNamedConstants(TDThing tdThing, SchemaNamer schemaNamer, Dictionary<string, Dictionary<string, TypedConstant>> schemaConstants)
        {
            IEnumerable<KeyValuePair<string, ValueTracker<TDDataSchema>>>? constDefs = tdThing.SchemaDefinitions?.Entries?.Where(d => d.Value.Value.Const?.Value != null);

            if (constDefs?.Any() ?? false)
            {
                string schemaName = schemaNamer.ConstantsSchema;
                if (!schemaConstants.TryGetValue(schemaName, out Dictionary<string, TypedConstant>? namedConstants))
                {
                    namedConstants = new();
                    schemaConstants[schemaName] = namedConstants;
                }

                foreach (var constDef in constDefs)
                {
                    if (constDef.Value.Value.Type?.Value.Value == TDValues.TypeString || constDef.Value.Value.Type?.Value.Value == TDValues.TypeNumber || constDef.Value.Value.Type?.Value.Value == TDValues.TypeInteger || constDef.Value.Value.Type?.Value.Value == TDValues.TypeBoolean)
                    {
                        string constName = schemaNamer.ChooseTitleOrName(constDef.Value.Value.Title?.Value.Value, constDef.Key);
                        namedConstants[constName] = new TypedConstant(new CodeName(constName), constDef.Value.Value.Type.Value.Value, constDef.Value.Value.Const!.Value!.Value, constDef.Value.Value.Description?.Value.Value);
                    }
                }
            }
        }

        private static void GenerateServiceEnvoys(SchemaNamer schemaNamer, CodeName serviceName, List<ActionSpec> actionSpecs, List<PropertySpec> propSpecs, List<EventSpec> eventSpecs, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms)
        {
            foreach (IEnvoyTemplateTransform transform in envoyFactory.GetServiceTransforms(schemaNamer, serviceName, actionSpecs, propSpecs, eventSpecs))
            {
                transforms[transform.FileName] = transform;
            }
        }

        private static void GenerateConstantEnvoys(Dictionary<string, Dictionary<string, TypedConstant>> schemaConstants, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms)
        {
            foreach (KeyValuePair<string, Dictionary<string, TypedConstant>> schemaConstant in schemaConstants)
            {
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetConstantTransforms(new CodeName(schemaConstant.Key), schemaConstant.Value.Values.OrderBy(c => c.Name.AsGiven).ToList()))
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

        private static void GenerateSerializationEnvoys(HashSet<string> typesToSerialize, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms)
        {
            foreach (string typeToSerialize in typesToSerialize)
            {
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetSerializationTransforms(typeToSerialize))
                {
                    transforms[transform.FileName] = transform;
                }
            }
        }
    }
}
