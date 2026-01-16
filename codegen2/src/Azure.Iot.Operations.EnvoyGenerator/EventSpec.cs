namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public record EventSpec(CodeName Name, CodeName Sender, CodeName Receiver, ITypeName Schema)
    {
        public EventSpec(SchemaNamer schemaNamer, string eventName, string schemaType, SerializationFormat format)
            : this(
                new CodeName(eventName),
                new CodeName(schemaNamer.GetEventSenderBinder(schemaType)),
                new CodeName(schemaNamer.GetEventReceiverBinder(schemaType)),
                EnvoyGeneratorSupport.GetTypeName(schemaType, format))
        {
        }
    }
}
