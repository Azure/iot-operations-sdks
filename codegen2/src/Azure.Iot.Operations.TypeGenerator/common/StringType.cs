namespace Azure.Iot.Operations.TypeGenerator
{
    internal class StringType : SchemaType
    {
        internal override SchemaKind Kind { get => SchemaKind.String; }

        internal StringType(bool orNull)
            : base(orNull)
        {
        }
    }
}
