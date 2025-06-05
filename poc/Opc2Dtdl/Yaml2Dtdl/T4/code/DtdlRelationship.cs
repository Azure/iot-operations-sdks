namespace Yaml2Dtdl
{
    using System.Linq;
    using OpcUaDigest;

    public partial class DtdlRelationship
    {
        private OpcUaDefinedType definedType;

        public string? Target { get; }

        public DtdlRelationship(OpcUaDefinedType definedType)
        {
            this.definedType = definedType;

            this.Target = GetTarget();
        }

        private string? GetTarget()
        {
            OpcUaDefinedType? typeDefinition = definedType.Contents.FirstOrDefault(c => c.Relationship == "HasTypeDefinition")?.DefinedType;
            if (typeDefinition == null || !typeDefinition.NodeId.Contains(':'))
            {
                return null;
            }

            return TypeConverter.GetModelId(typeDefinition);
        }
    }
}
