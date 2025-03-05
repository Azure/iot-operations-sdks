namespace Azure.Iot.Operations.ProtocolCompiler
{
    using DTDLParser;

    public static class CommonSchemaSupport
    {
        public static CodeName? GetNamespace(Dtmi schemaId, CodeName? sharedPrefix, CodeName? altNamespace = null)
        {
            return sharedPrefix?.AsDtmi != null && schemaId.AbsoluteUri.StartsWith(sharedPrefix.AsDtmi.AbsoluteUri) ? sharedPrefix : altNamespace;
        }
    }
}
