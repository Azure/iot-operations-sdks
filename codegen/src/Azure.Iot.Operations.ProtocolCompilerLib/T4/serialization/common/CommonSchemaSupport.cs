namespace Azure.Iot.Operations.ProtocolCompilerLib
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
