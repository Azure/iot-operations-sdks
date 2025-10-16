namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class ActionEnvoyGenerator
    {
        internal static void GenerateActionEnvoys(TDThing tdThing, SchemaNamer schemaNamer, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms, Dictionary<string, ErrorSpec> errorSpecs, HashSet<string> typesToSerialize)
        {
            foreach (KeyValuePair<string, TDAction> actionKvp in tdThing.Actions ?? new())
            {
                FormInfo? actionForm = FormInfo.CreateFromForm(actionKvp.Value.Forms?.FirstOrDefault(f => f.Op == TDValues.OpInvokeAction || f.Op == TDValues.OpQueryAction), tdThing.SchemaDefinitions);
                if (actionForm?.TopicPattern != null && actionForm.Format != SerializationFormat.None)
                {
                    string? inputSchemaType = actionKvp.Value.Input != null ? schemaNamer.GetActionInSchema(actionKvp.Value.Input.Title, actionKvp.Key) : null;
                    string? outArgsType = actionKvp.Value.Output != null ? schemaNamer.GetActionOutSchema(actionKvp.Value.Output.Title, actionKvp.Key) : null;
                    string? outputSchemaType = actionForm.ErrorRespSchema != null ? schemaNamer.GetActionRespSchema(actionKvp.Key) : outArgsType;
                    string? errSchemaName = schemaNamer.ChooseTitleOrName(actionForm.ErrorRespSchema?.Title, actionForm.ErrorRespName);

                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetActionTransforms(
                        schemaNamer,
                        tdThing.Id!,
                        actionKvp.Key,
                        inputSchemaType,
                        outputSchemaType,
                        actionForm.Format,
                        actionForm.ServiceGroupId,
                        actionForm.TopicPattern,
                        actionKvp.Value.Idempotent,
                        actionKvp.Value.Output?.Properties?.Keys?.ToList() ?? new(),
                        actionKvp.Value.Output?.Required?.ToList() ?? new(),
                        outArgsType,
                        actionForm.ErrorRespName,
                        errSchemaName,
                        actionForm.HeaderCodeName,
                        schemaNamer.ChooseTitleOrName(actionForm.HeaderCodeSchema?.Title, actionForm.HeaderCodeName),
                        actionForm.HeaderInfoName,
                        schemaNamer.ChooseTitleOrName(actionForm.HeaderInfoSchema?.Title, actionForm.HeaderInfoName),
                        actionForm.HeaderCodeSchema?.Enum?.ToList()))
                    {
                        transforms[transform.FileName] = transform;
                    }

                    if (inputSchemaType != null)
                    {
                        typesToSerialize.Add(inputSchemaType);
                    }

                    if (outputSchemaType != null)
                    {
                        typesToSerialize.Add(outputSchemaType);
                    }

                    if (actionForm.ErrorRespSchema != null)
                    {
                        if (outArgsType != null)
                        {
                            typesToSerialize.Add(outArgsType);
                        }

                        typesToSerialize.Add(errSchemaName!);

                        ErrorSpec errorSpec = new ErrorSpec(
                            errSchemaName!,
                            actionForm.HeaderCodeName,
                            schemaNamer.ChooseTitleOrName(actionForm.HeaderCodeSchema?.Title, actionForm.HeaderCodeName),
                            actionForm.HeaderInfoName,
                            schemaNamer.ChooseTitleOrName(actionForm.HeaderInfoSchema?.Title, actionForm.HeaderInfoName),
                            actionForm.ErrorRespSchema.Description ?? "The action could not be completed",
                            actionForm.ErrorRespSchema.ErrorMessage,
                            actionForm.ErrorRespSchema.Required?.Contains(actionForm.ErrorRespSchema.ErrorMessage ?? string.Empty) ?? false);

                        errorSpecs[errSchemaName!] = errorSpec;
                    }
                }
            }
        }
    }
}
