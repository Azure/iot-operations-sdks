namespace Azure.Iot.Operations.SchemaGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class ActionSchemaGenerator
    {
        private const string InputOutputType = "object";

        internal static void GenerateActionSchemas(ErrorReporter errorReporter, TDThing tdThing, string dirName, SchemaNamer schemaNamer, string projectName, Dictionary<string, List<SchemaSpec>> schemaSpecs, Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            foreach (KeyValuePair<string, ValueTracker<TDAction>> actionKvp in tdThing.Actions?.Entries ?? new())
            {
                TDAction? action = actionKvp.Value.Value;
                if (action != null)
                {
                    ProcessAction(
                        errorReporter,
                        schemaNamer,
                        actionKvp.Key,
                        action,
                        projectName,
                        dirName,
                        tdThing.SchemaDefinitions?.Entries,
                        schemaSpecs,
                        referencedSchemas);
                }
            }
        }

        private static void ProcessAction(
            ErrorReporter errorReporter,
            SchemaNamer schemaNamer,
            string actionName,
            TDAction tdAction,
            string projectName,
            string dirName,
            Dictionary<string, ValueTracker<TDDataSchema>>? schemaDefinitions,
            Dictionary<string, List<SchemaSpec>> schemaSpecs,
            Dictionary<string, HashSet<SerializationFormat>> referencedSchemas)
        {
            FormInfo? actionForm = FormInfo.CreateFromForm(errorReporter, tdAction.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpInvokeAction) ?? false)?.Value, schemaDefinitions);
            actionForm ??= FormInfo.CreateFromForm(errorReporter, tdAction.Forms?.Elements?.FirstOrDefault(f => f.Value.Op == null)?.Value, schemaDefinitions);

            if (actionForm?.TopicPattern != null)
            {
                ValueTracker<StringHolder>? inputRef = tdAction.Input?.Value?.Ref;
                if (inputRef != null)
                {
                    string inputSchemaName = schemaNamer.GetActionInSchema(null, actionName);
                    if (!schemaSpecs.TryGetValue(inputSchemaName, out List<SchemaSpec>? inputSpecs))
                    {
                        inputSpecs = new List<SchemaSpec>();
                        schemaSpecs[inputSchemaName] = inputSpecs;
                    }
                    inputSpecs.Add(new AliasSpec(null, InputOutputType, inputRef.Value.Value, actionForm.Format, inputSchemaName, dirName, TokenIndex: -1));
                    errorReporter.RegisterTypedReferenceFromThing(inputRef.TokenIndex, InputOutputType, inputRef.Value.Value);
                }
                else if (tdAction.Input?.Value != null && tdAction.Input.Value.Type?.Value.Value != TDValues.TypeNull)
                {
                    string inputSchemaName = schemaNamer.GetActionInSchema(tdAction.Input.Value, actionName);
                    ObjectSpec inputObjectSpec = ObjectSpec.CreateFromDataSchema(errorReporter, schemaNamer, tdAction.Input, actionForm.Format, inputSchemaName, tdAction.Input.Value.Description?.Value.Value ?? $"Input arguments for action '{actionName}'");
                    if (!schemaSpecs.TryGetValue(inputSchemaName, out List<SchemaSpec>? inputSpecs))
                    {
                        inputSpecs = new List<SchemaSpec>();
                        schemaSpecs[inputSchemaName] = inputSpecs;
                    }
                    inputSpecs.Add(inputObjectSpec);
                }

                Dictionary<string, FieldSpec> responseFields = new();
                ValueTracker<StringHolder>? outputRef = tdAction.Output?.Value?.Ref;
                if (outputRef != null)
                {
                    string outputSchemaName = schemaNamer.GetActionOutSchema(null, actionName);
                    if (!schemaSpecs.TryGetValue(outputSchemaName, out List<SchemaSpec>? outputSpecs))
                    {
                        outputSpecs = new List<SchemaSpec>();
                        schemaSpecs[outputSchemaName] = outputSpecs;
                    }
                    outputSpecs.Add(new AliasSpec(null, InputOutputType, outputRef.Value.Value, actionForm.Format, outputSchemaName, dirName, TokenIndex: -1));
                    errorReporter.RegisterTypedReferenceFromThing(outputRef.TokenIndex, InputOutputType, outputRef.Value.Value);
                }
                else if (tdAction.Output?.Value != null && tdAction.Output.Value.Type?.Value.Value != TDValues.TypeNull)
                {
                    string outputSchemaName = schemaNamer.GetActionOutSchema(tdAction.Output.Value, actionName);
                    ObjectSpec outputObjectSpec = ObjectSpec.CreateFromDataSchema(errorReporter, schemaNamer, tdAction.Output, actionForm.Format, outputSchemaName, tdAction.Output.Value.Description?.Value.Value ?? $"Output arguments for action '{actionName}'");
                    if (!schemaSpecs.TryGetValue(outputSchemaName, out List<SchemaSpec>? outputSpecs))
                    {
                        outputSpecs = new List<SchemaSpec>();
                        schemaSpecs[outputSchemaName] = outputSpecs;
                    }
                    outputSpecs.Add(outputObjectSpec);
                    responseFields = outputObjectSpec.Fields.ToDictionary(f => f.Key, f => f.Value with { Require = false });
                }

                if (actionForm?.ErrorRespSchema != null)
                {
                    responseFields[schemaNamer.GetActionRespErrorField(actionName, actionForm.ErrorRespName!)] = new FieldSpec(
                        $"Read error for the '{actionName}' Action.",
                        actionForm.ErrorRespSchema,
                        Require: false,
                        BackupSchemaName: actionForm.ErrorRespName!,
                        Base: string.Empty);

                    string respSchemaName = schemaNamer.GetActionRespSchema(actionName);
                    ObjectSpec propReadRespObjectSpec = new(
                        tdAction.Output?.Value.Description?.Value.Value ?? $"Response to a '{actionName}' Action.",
                        responseFields,
                        actionForm.Format,
                        respSchemaName,
                        TokenIndex: -1);
                    if (!schemaSpecs.TryGetValue(respSchemaName, out List<SchemaSpec>? respSpecs))
                    {
                        respSpecs = new List<SchemaSpec>();
                        schemaSpecs[respSchemaName] = respSpecs;
                    }
                    respSpecs.Add(propReadRespObjectSpec);

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
