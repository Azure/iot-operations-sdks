namespace Azure.Iot.Operations.SchemaGenerator
{
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal record FieldSpec(string Description, ValueTracker<TDDataSchema> Schema, bool Require, string BackupSchemaName, string Base, bool Fragment = false, bool ForceOption = false)
    {
        internal static FieldSpec CreateFixed(string title, string description, string backupSchemaName)
        {
            return new FieldSpec(
                description,
                new ValueTracker<TDDataSchema>
                {
                    Value = new TDDataSchema
                    {
                        Title = new ValueTracker<StringHolder> { Value = new StringHolder { Value = title } },
                        Type = new ValueTracker<StringHolder> { Value = new StringHolder { Value = TDValues.TypeObject } },
                    },
                },
                Require: false,
                backupSchemaName,
                string.Empty);
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
                Schema == other.Schema &&
                Require == other.Require &&
                BackupSchemaName == other.BackupSchemaName &&
                Fragment == other.Fragment;
        }
    }
}
