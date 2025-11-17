namespace Azure.Iot.Operations.TypeGenerator
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public class TypeGenerator
    {
        private ISchemaStandardizer schemaStandardizer;
        private ITypeGenerator typeGenerator;
        private ErrorLog errorLog;

        public TypeGenerator(SerializationFormat serializationFormat, TargetLanguage targetLanguage, TypeNamer typeNamer, ErrorLog errorLog)
        {
            this.schemaStandardizer = serializationFormat switch
            {
                SerializationFormat.Json => new JsonSchemaStandardizer(typeNamer),
                _ => throw new NotSupportedException($"Serialization format {serializationFormat} is not supported."),
            };

            this.typeGenerator = targetLanguage switch
            {
                TargetLanguage.CSharp => new DotNetTypeGenerator(),
                TargetLanguage.Rust => new RustTypeGenerator(),
                _ => throw new NotSupportedException($"Target language {targetLanguage} is not supported."),
            };

            this.errorLog = errorLog;
        }

        public List<GeneratedItem> GenerateTypes(Dictionary<string, string> schemaTextsByName, CodeName genNamespace, string projectName, string srcSubdir)
        {
            List<GeneratedItem> generatedTypes = new();

            CycleBreaker cycleBreaker = new (this.typeGenerator.TargetLanguage);

            if (schemaStandardizer.TryGetStandardizedSchemas(schemaTextsByName, this.errorLog, out List<SchemaType> standardizedSchemas))
            {
                foreach (SchemaType schemaType in standardizedSchemas)
                {
                    cycleBreaker.AddIndirectionAsNeeded(schemaType);

                    generatedTypes.Add(this.typeGenerator.GenerateTypeFromSchema(schemaType, projectName, genNamespace, schemaStandardizer.SerializationFormat, srcSubdir));
                }
            }

            return generatedTypes;
        }
    }
}
