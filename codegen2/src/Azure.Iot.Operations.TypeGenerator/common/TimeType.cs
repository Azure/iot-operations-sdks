namespace Azure.Iot.Operations.TypeGenerator
{
    public class TimeType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Time; }

        public TimeType()
        {
        }
    }
}
