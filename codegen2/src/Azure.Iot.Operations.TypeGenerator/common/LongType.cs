namespace Azure.Iot.Operations.TypeGenerator
{
    internal class LongType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.Long; }

        internal LongType(bool orNull)
            : base(orNull)
        {
        }
    }
}
