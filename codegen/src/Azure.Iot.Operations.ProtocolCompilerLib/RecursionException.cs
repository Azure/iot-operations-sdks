namespace Azure.Iot.Operations.ProtocolCompilerLib
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
