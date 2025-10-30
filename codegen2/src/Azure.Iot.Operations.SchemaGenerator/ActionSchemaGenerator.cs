﻿namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class ActionSchemaGenerator
    {
        internal static void GenerateActionSchemas(TDThing tdThing, SchemaNamer schemaNamer, string projectName, Dictionary<string, SchemaSpec> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
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
            FormInfo? actionForm = FormInfo.CreateFromForm(tdAction.Forms?.FirstOrDefault(f => (f.Op?.Values.Contains(TDValues.OpInvokeAction) ?? false) || (f.Op?.Values.Contains(TDValues.OpQueryAction) ?? false)), schemaDefinitions);
            actionForm ??= FormInfo.CreateFromForm(tdAction.Forms?.FirstOrDefault(f => f.Op == null), schemaDefinitions);

            if (actionForm?.TopicPattern != null)
            {
                if (tdAction.Input != null)
                {
                    string inputSchemaName = schemaNamer.GetActionInSchema(tdAction.Input.Title, actionName);
                    ObjectSpec inputObjectSpec = ObjectSpec.CreateFromDataSchema(schemaNamer, tdAction.Input, actionForm.Format, inputSchemaName, tdAction.Input.Description ?? $"Input arguments for action '{actionName}'");
                    schemaSpecs[inputSchemaName] = inputObjectSpec;
                }

                Dictionary<string, FieldSpec> responseFields = new();
                if (tdAction.Output != null)
                {
                    string outputSchemaName = schemaNamer.GetActionOutSchema(tdAction.Output.Title, actionName);
                    ObjectSpec outputObjectSpec = ObjectSpec.CreateFromDataSchema(schemaNamer, tdAction.Output, actionForm.Format, outputSchemaName, tdAction.Output.Description ?? $"Output arguments for action '{actionName}'");
                    schemaSpecs[outputSchemaName] = outputObjectSpec;
                    responseFields = outputObjectSpec.Fields.ToDictionary(f => f.Key, f => f.Value with { Require = false });
                }

                if (actionForm?.ErrorRespSchema != null)
                {
                    responseFields[schemaNamer.GetActionRespErrorField(actionName, actionForm.ErrorRespName!)] = new FieldSpec(
                        tdAction.Description ?? $"Read error for the '{actionName}' Action.",
                        actionForm.ErrorRespSchema,
                        BackupSchemaName: actionForm.ErrorRespName!,
                        Require: false);

                    string respSchemaName = schemaNamer.GetActionRespSchema(actionName);
                    ObjectSpec propReadRespObjectSpec = new(
                        tdAction.Description ?? $"Response to a '{actionName}' Action.",
                        responseFields,
                        actionForm.Format,
                        respSchemaName);
                    schemaSpecs[respSchemaName] = propReadRespObjectSpec;

                    SchemaGenerationSupport.AddSchemaReference(actionForm.ErrorRespName!, actionForm.ErrorRespFormat, referencedSchemas);
                }

                if (actionForm?.HeaderInfoSchema != null)
                {
                    SchemaGenerationSupport.AddSchemaReference(actionForm.HeaderInfoName!, actionForm.HeaderInfoFormat, referencedSchemas);
                }

                if (actionForm?.HeaderCodeSchema != null)
                {
                    SchemaGenerationSupport.AddSchemaReference(actionForm.HeaderCodeName!, SerializationFormat.Json, referencedSchemas);
                }
            }
        }
    }
}
