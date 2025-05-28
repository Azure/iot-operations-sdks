namespace OpcUaDigest
{
    public class EdgeMetadata
    {
        public EdgeMetadata(OpcUaDefinedType definedType, string relationship)
            : this(definedType.NodeType, definedType.NodeId, definedType.BrowseName, relationship)
        {
        }

        public EdgeMetadata(string nodeType, string nodeId, string browseName, string relationship)
        {
            NodeType = nodeType;
            NodeId = nodeId;
            BrowseName = browseName;
            Relationship = relationship;
        }

        public string NodeType { get; }

        public string NodeId { get; }

        public string BrowseName { get; }

        public string Relationship { get; }
    }
}
