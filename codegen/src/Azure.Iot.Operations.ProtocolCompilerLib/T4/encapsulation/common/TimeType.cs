namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    public class TimeType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Time; }

        public TimeType()
        {
        }
    }
}
