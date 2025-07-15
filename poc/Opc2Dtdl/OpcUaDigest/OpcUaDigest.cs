namespace OpcUaDigest
{
    using System.Collections.Generic;

    public class OpcUaDigest
    {
        public string SpecUri { get; set; } = string.Empty;

        public string SpecVer { get; set; } = string.Empty;

        public List<OpcUaDataType> DataTypes { get; set; } = new ();

        public Dictionary<string, OpcUaDefinedType> DefinedTypes { get; set; } = new ();
    }
}
