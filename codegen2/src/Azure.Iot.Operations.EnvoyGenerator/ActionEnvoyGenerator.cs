namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class ActionEnvoyGenerator
    {
        internal static List<ActionSpec> GenerateActionEnvoys(TDThing tdThing, SchemaNamer schemaNamer, CodeName serviceName, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms, Dictionary<string, ErrorSpec> errorSpecs, HashSet<string> typesToSerialize)
        {
            List<ActionSpec> actionSpecs = new();

            foreach (KeyValuePair<string, TDAction> actionKvp in tdThing.Actions ?? new())
            {
                FormInfo? actionForm = FormInfo.CreateFromForm(actionKvp.Value.Forms?.FirstOrDefault(f => (f.Op?.Values.Contains(TDValues.OpInvokeAction) ?? false) || (f.Op?.Values.Contains(TDValues.OpQueryAction) ?? false)), tdThing.SchemaDefinitions);
                actionForm ??= FormInfo.CreateFromForm(actionKvp.Value.Forms?.FirstOrDefault(f => f.Op == null), tdThing.SchemaDefinitions);

                if (actionForm?.TopicPattern != null && actionForm.Format != SerializationFormat.None)
                {
                    string? inputSchemaType = actionKvp.Value.Input != null ? schemaNamer.GetActionInSchema(actionKvp.Value.Input, actionKvp.Key) : null;
                    string? outArgsType = actionKvp.Value.Output != null ? schemaNamer.GetActionOutSchema(actionKvp.Value.Output, actionKvp.Key) : null;
                    string? outputSchemaType = actionForm.ErrorRespSchema != null ? schemaNamer.GetActionRespSchema(actionKvp.Key) : outArgsType;
                    string? errSchemaName = schemaNamer.ChooseTitleOrName(actionForm.ErrorRespSchema?.Title, actionForm.ErrorRespName);

                    List<string> normalResultNames = actionKvp.Value.Output?.Properties?.Keys?.ToList() ?? new();
                    List<string> normalRequiredNames = actionKvp.Value.Output?.Required?.ToList() ?? new();
                    string? headerCodeSchema = schemaNamer.ChooseTitleOrName(actionForm.HeaderCodeSchema?.Title, actionForm.HeaderCodeName);
                    string? headerInfoSchema = schemaNamer.ChooseTitleOrName(actionForm.HeaderInfoSchema?.Title, actionForm.HeaderInfoName);

                    bool doesTargetExecutor = DoesTopicReferToExecutor(actionForm.TopicPattern);

                    actionSpecs.Add(new ActionSpec(
                        schemaNamer,
                        actionKvp.Key,
                        inputSchemaType,
                        outputSchemaType,
                        actionForm.Format,
                        normalResultNames,
                        normalRequiredNames,
                        outArgsType,
                        actionForm.ErrorRespName,
                        errSchemaName,
                        actionForm.HeaderCodeName,
                        headerCodeSchema,
                        actionForm.HeaderInfoName,
                        headerInfoSchema,
                        doesTargetExecutor));

                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetActionTransforms(
                        schemaNamer,
                        tdThing.Id!,
                        serviceName,
                        actionKvp.Key,
                        inputSchemaType,
                        outputSchemaType,
                        actionForm.Format,
                        actionForm.ServiceGroupId,
                        actionForm.TopicPattern,
                        actionKvp.Value.Idempotent,
                        normalResultNames,
                        normalRequiredNames,
                        outArgsType,
                        actionForm.ErrorRespName,
                        errSchemaName,
                        actionForm.HeaderCodeName,
                        headerCodeSchema,
                        actionForm.HeaderInfoName,
                        headerInfoSchema,
                        actionForm.HeaderCodeSchema?.Enum?.ToList(),
                        doesTargetExecutor))
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
                            actionForm.ErrorRespSchema.Description ?? "The action could not be completed",
                            actionForm.ErrorRespSchema.ErrorMessage,
                            actionForm.ErrorRespSchema.Required?.Contains(actionForm.ErrorRespSchema.ErrorMessage ?? string.Empty) ?? false,
                            actionForm.HeaderCodeName,
                            schemaNamer.ChooseTitleOrName(actionForm.HeaderCodeSchema?.Title, actionForm.HeaderCodeName),
                            actionForm.HeaderInfoName,
                            schemaNamer.ChooseTitleOrName(actionForm.HeaderInfoSchema?.Title, actionForm.HeaderInfoName));

                        errorSpecs[errSchemaName!] = errorSpec;
                    }
                }
            }

            return actionSpecs;
        }

        private static bool DoesTopicReferToExecutor(string? topic)
        {
            return topic != null && topic.Contains(MqttTopicTokens.CommandExecutorId);
        }
    }
}
