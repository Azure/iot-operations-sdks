namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    using System;

    public class RecursionException : Exception
    {
        public CodeName SchemaName { get; }

        public RecursionException(CodeName schemaName)
            : base($"Schema {schemaName.AsGiven} refers to itself")
        {
            SchemaName = schemaName;
        }
    }
}
