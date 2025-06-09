namespace OpcUaDigest
{
    using System.Collections.Generic;

    public class OpcUaDefinedType
    {
        public OpcUaDefinedType(string nodeType, string nodeId, string browseName, string? datatype = null, int valueRank = 0, int accessLevel = 0)
        {
            NodeType = nodeType;
            NodeId = nodeId;
            BrowseName = browseName;
            Datatype = datatype;
            ValueRank = valueRank;
            AccessLevel = accessLevel;
            Contents = new List<OpcUaContent>();
            Arguments = new Dictionary<string, (string?, int)>();
            UnitId = null;
        }

        public string NodeType { get; }

        public string NodeId { get; }

        public string BrowseName { get; }

        public string? Datatype { get; }

        public int ValueRank { get; }

        public int AccessLevel { get; }

        public List<OpcUaContent> Contents { get; }

        public Dictionary<string, (string?, int)> Arguments { get; }

        public string? UnitId { get; set; }

        public IEnumerable<TraversedType> Traverse() => Traverse(new List<EdgeMetadata>());

        private IEnumerable<TraversedType> Traverse(List<EdgeMetadata> traversalPath)
        {
            TraversedType traversedType = new TraversedType(traversalPath, this);
            yield return traversedType;

            foreach (OpcUaContent content in Contents)
            {
                List<EdgeMetadata> childTraversalPath = new List<EdgeMetadata>(traversalPath);
                childTraversalPath.Add(new EdgeMetadata(this, content.Relationship));

                foreach (TraversedType descendantTraversedType in content.DefinedType.Traverse(childTraversalPath))
                {
                    yield return descendantTraversedType;
                }
            }
        }
    }
}
