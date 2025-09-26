namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class ActionSchemaGenerator
    {
        internal static void GenerateActionSchemas(TDThing tdThing, SchemaNamer schemaNamer, string projectName, Dictionary<string, SchemaSpec> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            Dictionary<string, FieldSpec> readValueFields = new();
            Dictionary<string, FieldSpec> writeValueFields = new();
            Dictionary<string, FieldSpec> readErrorFields = new();
            Dictionary<string, FieldSpec> writeErrorFields = new();
            HashSet<string> readErrorSchemaNames = new();
            HashSet<string> writeErrorSchemaNames = new();

            if (tdThing.Actions != null)
            {
                foreach (KeyValuePair<string, TDAction> propKvp in tdThing.Actions)
                {
                    ProcessAction(
                        schemaNamer,
                        propKvp.Key,
                        propKvp.Value,
                        projectName,
                        tdThing.SchemaDefinitions,
                        schemaSpecs,
                        referencedSchemas);
                }
            }
        }

        private static void ProcessAction(
            SchemaNamer schemaNamer,
            string actionName,
            TDAction tdAction,
            string projectName,
            Dictionary<string, TDDataSchema>? schemaDefinitions,
            Dictionary<string, SchemaSpec> schemaSpecs,
            Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            FormInfo? actionForm = FormInfo.CreateFromForm(tdAction.Forms?.FirstOrDefault(f => f.Op == TDValues.OpInvokeAction || f.Op == TDValues.OpQueryAction), schemaDefinitions);

            if (actionForm?.TopicPattern != null)
            {
                if (tdAction.Input != null)
                {
                    string inputSchemaName = tdAction.Input.Title ?? schemaNamer.GetActionInSchema(actionName);
                    ObjectSpec inputObjectSpec = ObjectSpec.CreateFromDataSchema(schemaNamer, tdAction.Input, actionForm.Format, inputSchemaName, tdAction.Input.Description ?? $"Input arguments for action '{actionName}'");
                    schemaSpecs[inputSchemaName] = inputObjectSpec;
                }

                Dictionary<string, FieldSpec> responseFields = new();
                if (tdAction.Output != null)
                {
                    string outputSchemaName = tdAction.Output.Title ?? schemaNamer.GetActionOutSchema(actionName);
                    ObjectSpec outputObjectSpec = ObjectSpec.CreateFromDataSchema(schemaNamer, tdAction.Output, actionForm.Format, outputSchemaName, tdAction.Output.Description ?? $"Output arguments for action '{actionName}'");
                    schemaSpecs[outputSchemaName] = outputObjectSpec;
                    responseFields = outputObjectSpec.Fields.ToDictionary(f => f.Key, f => f.Value with { Require = false });
                }

                if (actionForm?.ErrorSchema != null)
                {
                    responseFields[schemaNamer.ActionRespErrorField] = new FieldSpec(
                        tdAction.Description ?? $"Read error for the '{actionName}' Action.",
                        actionForm.ErrorSchema,
                        BackupSchemaName: actionForm.ErrorSchemaName!,
                        Require: false);

                    string respSchemaName = schemaNamer.GetActionRespSchema(actionName);
                    ObjectSpec propReadRespObjectSpec = new(
                        tdAction.Description ?? $"Response to a '{actionName}' Action.",
                        responseFields,
                        actionForm.Format,
                        respSchemaName);
                    schemaSpecs[respSchemaName] = propReadRespObjectSpec;

                    SchemaGenerationSupport.AddSchemaReference(actionForm.ErrorSchemaName!, actionForm.ErrorSchemaFormat, referencedSchemas);
                }

                if (actionForm?.HeaderSchema != null)
                {
                    SchemaGenerationSupport.AddSchemaReference(actionForm.HeaderSchemaName!, actionForm.HeaderSchemaFormat, referencedSchemas);
                }
            }
        }
    }
}
