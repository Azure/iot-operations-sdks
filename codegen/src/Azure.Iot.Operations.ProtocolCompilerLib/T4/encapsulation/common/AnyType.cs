namespace Azure.Iot.Operations.ProtocolCompilerLib
{
    /// <summary>
    /// A value whose schema permits any JSON value, such as a heterogeneous array element.
    /// </summary>
    public class AnyType : SchemaType
    {
        public override SchemaKind Kind { get => SchemaKind.Any; }

        public AnyType()
        {
        }
    }
}
