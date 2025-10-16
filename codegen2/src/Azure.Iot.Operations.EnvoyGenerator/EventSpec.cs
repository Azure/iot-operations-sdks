namespace Azure.Iot.Operations.EnvoyGenerator
{
    using Azure.Iot.Operations.CodeGeneration;

    public record EventSpec(CodeName Name, CodeName Sender, CodeName Receiver, ITypeName Schema)
    {
        public EventSpec(SchemaNamer schemaNamer, string eventName, string schemaType)
            : this(
                new CodeName(eventName),
                new CodeName(schemaNamer.GetEventSenderBinder(eventName)),
                new CodeName(schemaNamer.GetEventReceiverBinder(eventName)),
                new CodeName(schemaType))
        {
        }
    }
}
