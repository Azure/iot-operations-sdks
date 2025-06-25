namespace OpcUaDigest
{
    using System;

    public abstract class OpcUaDataType
    {
        public string[] UADataType { get; set; } = Array.Empty<string>();

        public string NodeId { get => UADataType[0]; }

        public string BrowseName { get => UADataType[1]; }

        public string? DisplayName { get; set; }

        public string? Description { get; set; }
    }
}
