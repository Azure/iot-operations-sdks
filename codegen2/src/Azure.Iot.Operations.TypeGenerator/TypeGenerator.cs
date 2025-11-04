namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public class TypeGenerator
    {
        private ISchemaStandardizer schemaStandardizer;
        private ITypeGenerator typeGenerator;

        public TypeGenerator(SerializationFormat serializationFormat, TargetLanguage targetLanguage)
        {
            this.schemaStandardizer = serializationFormat switch
            {
                SerializationFormat.Json => new JsonSchemaStandardizer(),
                _ => throw new NotSupportedException($"Serialization format {serializationFormat} is not supported."),
            };

            this.typeGenerator = targetLanguage switch
            {
                TargetLanguage.CSharp => new DotNetTypeGenerator(),
                TargetLanguage.Rust => new RustTypeGenerator(),
                _ => throw new NotSupportedException($"Target language {targetLanguage} is not supported."),
            };
        }

        public List<GeneratedItem> GenerateTypes(Dictionary<string, string> schemaTextsByName, CodeName genNamespace, string projectName, string srcSubdir)
        {
            List<GeneratedItem> generatedTypes = new();

            foreach (SchemaType schemaType in schemaStandardizer.GetStandardizedSchemas(schemaTextsByName))
            {
                generatedTypes.Add(this.typeGenerator.GenerateTypeFromSchema(schemaType, projectName, genNamespace, schemaStandardizer.SerializationFormat, srcSubdir));
            }

            return generatedTypes;
        }
    }
}
