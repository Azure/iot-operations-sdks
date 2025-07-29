namespace Azure.Iot.Operations.Services.SchemaRegistry.Models
{
    public partial class SchemaRegistryErrorException : Exception
    {
        public SchemaRegistryErrorException(SchemaRegistryError schemaRegistryError)
            : base(schemaRegistryError.Message)
        {
            SchemaRegistryError = schemaRegistryError;
        }

        public SchemaRegistryErrorException(SchemaRegistryError schemaRegistryError, Exception innerException)
            : base(schemaRegistryError.Message, innerException)
        {
            SchemaRegistryError = schemaRegistryError;
        }

        public SchemaRegistryError SchemaRegistryError { get; set; }
    }
}
