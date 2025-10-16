namespace Azure.Iot.Operations.SchemaGenerator
{
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal record FieldSpec(string Description, TDDataSchema Schema, bool Require, string BackupSchemaName, bool Fragment = false, bool ForceOption = false)
    {
        internal static FieldSpec CreateFixed(string title, string description, string backupSchemaName)
        {
            return new FieldSpec(
                description,
                new TDDataSchema
                {
                    Title = title,
                    Type = TDValues.TypeObject,
                    AdditionalProperties = new TDAdditionalPropSpecifier { Boolean = false },
                },
                Require: false,
                backupSchemaName);
        }

        public override int GetHashCode()
        {
            return (Description, Schema, Require, BackupSchemaName, Fragment).GetHashCode();
        }

        public virtual bool Equals(FieldSpec? other)
        {
            if (other == null)
            {
                return false;
            }

            return Description == other.Description &&
                Schema.Equals(other.Schema) &&
                Require == other.Require &&
                BackupSchemaName == other.BackupSchemaName &&
                Fragment == other.Fragment;
        }
    }
}
