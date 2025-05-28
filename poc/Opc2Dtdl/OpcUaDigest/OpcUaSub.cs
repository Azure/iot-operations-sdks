namespace OpcUaDigest
{
    using System.Collections.Generic;

    public class OpcUaSub : OpcUaDataType
    {
        public List<string> Bases { get; set; } = new ();

        public override string ToString()
        {
            return $"Sub: {string.Join(",", Bases)}";
        }
    }
}
