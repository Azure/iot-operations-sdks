namespace OpcUaDigest
{
    using System.Collections.Generic;

    public class OpcUaEnum : OpcUaDataType
    {
        public Dictionary<string, int> Enums { get; set; } = new ();

        public override string ToString()
        {
            return $"Enum: {string.Join(",", Enums.Keys)}";
        }
    }
}
