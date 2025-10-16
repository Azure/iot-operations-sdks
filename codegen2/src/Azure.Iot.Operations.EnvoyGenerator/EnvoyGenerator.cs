namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    public static class EnvoyGenerator
    {
        public static List<GeneratedItem> GenerateEnvoys(TDThing tdThing, SchemaNamer schemaNamer, TargetLanguage targetLanguage, string genNamespace, string projectName, string serviceName, string sdkPath, List<string> typeFileNames)
        {
            EnvoyTransformFactory envoyFactory = new(tdThing.Id!, targetLanguage, new CodeName(genNamespace), projectName, new CodeName(serviceName), generateClient: true, generateServer: true);

            List<SerializationFormat> serializationFormats = ThingSupport.GetSerializationFormats(tdThing);

            List<IEnvoyTemplateTransform> transforms = new();
            Dictionary<string, ErrorSpec> errorSpecs = new();
            HashSet<string> typesToSerialize = new();

            ActionEnvoyGenerator.GenerateActionEnvoys(tdThing, schemaNamer, envoyFactory, transforms, errorSpecs, typesToSerialize);
            EventEnvoyGenerator.GenerateEventEnvoys(tdThing, schemaNamer, envoyFactory, transforms);
            GenerateErrorEnvoys(errorSpecs, envoyFactory, transforms);
            GenerateSerializationEnvoys(typesToSerialize, envoyFactory, transforms);

            List<GeneratedItem> generatedEnvoys = new();

            foreach (IEnvoyTemplateTransform service in envoyFactory.GetServiceTransforms(transforms.Select(t => t.FileName).Concat(typeFileNames).ToList()))
            {
                generatedEnvoys.Add(new GeneratedItem(service.TransformText(), service.FileName, service.FolderPath));
            }

            foreach (IEnvoyTemplateTransform transform in transforms)
            {
                generatedEnvoys.Add(new GeneratedItem(transform.TransformText(), transform.FileName, transform.FolderPath));
            }

            foreach (IEnvoyTemplateTransform project in envoyFactory.GetProjectTransforms(serializationFormats, sdkPath, generateProject: true))
            {
                generatedEnvoys.Add(new GeneratedItem(project.TransformText(), project.FileName, project.FolderPath));
            }

            foreach (IEnvoyTemplateTransform resource in envoyFactory.GetResourceTransforms(serializationFormats))
            {
                generatedEnvoys.Add(new GeneratedItem(resource.TransformText(), resource.FileName, resource.FolderPath));
            }

            return generatedEnvoys;
        }

        private static void GenerateErrorEnvoys(Dictionary<string, ErrorSpec> errorSpecs, EnvoyTransformFactory envoyFactory, List<IEnvoyTemplateTransform> transforms)
        {
            foreach (KeyValuePair<string, ErrorSpec> errorSpec in errorSpecs)
            {
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetErrorTransforms(errorSpec.Value))
                {
                    transforms.Add(transform);
                }
            }
        }

        private static void GenerateSerializationEnvoys(HashSet<string> typesToSerialize, EnvoyTransformFactory envoyFactory, List<IEnvoyTemplateTransform> transforms)
        {
            foreach (string typeToSerialize in typesToSerialize)
            {
                foreach (IEnvoyTemplateTransform transform in envoyFactory.GetSerializationTransforms(typeToSerialize))
                {
                    transforms.Add(transform);
                }
            }
        }
    }
}
