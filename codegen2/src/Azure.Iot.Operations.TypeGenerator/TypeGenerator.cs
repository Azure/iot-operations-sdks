namespace Azure.Iot.Operations.TypeGenerator
{
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.Serialization;

    public class TypeGenerator
    {
        private SerializationFormat serializationFormat;
        private TargetLanguage targetLanguage;

        public TypeGenerator(SerializationFormat serializationFormat, TargetLanguage targetLanguage)
        {
            this.serializationFormat = serializationFormat;
            this.targetLanguage = targetLanguage;
        }
    }
}
