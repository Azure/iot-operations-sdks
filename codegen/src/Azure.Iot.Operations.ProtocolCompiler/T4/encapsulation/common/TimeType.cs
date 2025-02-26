namespace Azure.Iot.Operations.ProtocolCompiler
{
    public class TimeType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Time; }

        public TimeType()
        {
        }
    }
}
