namespace OpcUaDigest
{
    public class OpcUaContent
    {
        public OpcUaContent(string relationship, OpcUaDefinedType definedType)
        {
            Relationship = relationship;
            DefinedType = definedType;
        }

        public string Relationship { get; }

        public OpcUaDefinedType DefinedType { get; }
    }
}
