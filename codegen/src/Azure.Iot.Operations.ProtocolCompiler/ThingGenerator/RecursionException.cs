namespace Azure.Iot.Operations.ProtocolCompiler
{
    using System;
    using DTDLParser;

    public class RecursionException : Exception
    {
        public Dtmi SchemaId { get; }

        public RecursionException(Dtmi schemaId)
            : base($"Schema {schemaId} refers to itself")
        {
            SchemaId = schemaId;
        }
    }
}
