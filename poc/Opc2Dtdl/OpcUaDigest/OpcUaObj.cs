namespace OpcUaDigest
{
    using System.Collections.Generic;

    public class OpcUaObj : OpcUaDataType
    {
        public Dictionary<string, (string?, int)> Fields { get; set; } = new();

        public override string ToString()
        {
            return $"Obj: {string.Join(",", Fields.Keys)}";
        }
    }
}
