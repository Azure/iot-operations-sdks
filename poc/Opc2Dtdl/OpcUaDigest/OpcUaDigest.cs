namespace OpcUaDigest
{
    using System.Collections.Generic;

    public class OpcUaDigest
    {
        public List<OpcUaDataType> DataTypes { get; set; } = new ();

        public Dictionary<string, OpcUaDefinedType> DefinedTypes { get; set; } = new ();
    }
}
