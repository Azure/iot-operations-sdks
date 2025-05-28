namespace OpcUaDigest
{
    using System.Collections.Generic;

    public class TraversedType
    {
        public TraversedType(List<EdgeMetadata> traversalPath, OpcUaDefinedType definedType)
        {
            TraversalPath = traversalPath;
            DefinedType = definedType;
        }

        public List<EdgeMetadata> TraversalPath { get; }

        public OpcUaDefinedType DefinedType { get; }
    }
}
