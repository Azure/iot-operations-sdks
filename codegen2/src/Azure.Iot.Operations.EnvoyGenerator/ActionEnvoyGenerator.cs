namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;
    using Azure.Iot.Operations.TDParser.Model;

    internal static class ActionEnvoyGenerator
    {
        internal static List<ActionSpec> GenerateActionEnvoys(ErrorReporter errorReporter, TDThing tdThing, SchemaNamer schemaNamer, CodeName serviceName, EnvoyTransformFactory envoyFactory, Dictionary<string, IEnvoyTemplateTransform> transforms, Dictionary<string, ErrorSpec> errorSpecs, Dictionary<SerializationFormat, HashSet<string>> formattedTypesToSerialize)
        {
            List<ActionSpec> actionSpecs = new();

            foreach (KeyValuePair<string, ValueTracker<TDAction>> actionKvp in tdThing.Actions?.Entries ?? new())
            {
                TDAction? action = actionKvp.Value.Value;
                if (action == null)
                {
                    continue;
                }

                FormInfo? actionForm = FormInfo.CreateFromForm(errorReporter, action.Forms?.Elements?.FirstOrDefault(f => f.Value.Op?.Elements?.Any(e => e.Value.Value == TDValues.OpInvokeAction) ?? false)?.Value, tdThing.SchemaDefinitions?.Entries);
                actionForm ??= FormInfo.CreateFromForm(errorReporter, action.Forms?.Elements?.FirstOrDefault(f => f.Value.Op == null)?.Value, tdThing.SchemaDefinitions?.Entries);

                if (actionForm?.TopicPattern != null && actionForm.Format != SerializationFormat.None)
                {
                    HashSet<string> typesToSerialize = formattedTypesToSerialize[actionForm.Format];

                    string? inputSchemaType = action.Input != null ? schemaNamer.GetActionInSchema(action.Input?.Value, actionKvp.Key) : null;
                    string? outArgsType = action.Output != null ? schemaNamer.GetActionOutSchema(action.Output?.Value, actionKvp.Key) : null;
                    string? outputSchemaType = actionForm.ErrorRespSchema != null ? schemaNamer.GetActionRespSchema(actionKvp.Key) : outArgsType;
                    string? errRespName = actionForm.ErrorRespName != null ? schemaNamer.GetActionRespErrorField(actionKvp.Key, actionForm.ErrorRespName) : null;
                    string? errSchemaName = schemaNamer.ChooseTitleOrName(actionForm.ErrorRespSchema?.Value.Title?.Value?.Value, actionForm.ErrorRespName);

                    List<string> normalResultNames = action.Output?.Value?.Properties?.Entries?.Keys?.ToList() ?? new();
                    List<ValueTracker<StringHolder>> normalRequiredNames = action.Output?.Value?.Required?.Elements?.ToList() ?? new();
                    string? headerCodeSchema = schemaNamer.ChooseTitleOrName(actionForm.HeaderCodeSchema?.Value.Title?.Value?.Value, actionForm.HeaderCodeName);
                    string? headerInfoSchema = schemaNamer.ChooseTitleOrName(actionForm.HeaderInfoSchema?.Value.Title?.Value?.Value, actionForm.HeaderInfoName);

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
                        errRespName,
                        errSchemaName,
                        actionForm.HeaderCodeName,
                        headerCodeSchema,
                        actionForm.HeaderInfoName,
                        headerInfoSchema,
                        doesTargetExecutor));

                    foreach (IEnvoyTemplateTransform transform in envoyFactory.GetActionTransforms(
                        schemaNamer,
                        tdThing.Id!.Value!.Value,
                        serviceName,
                        actionKvp.Key,
                        inputSchemaType,
                        outputSchemaType,
                        actionForm.Format,
                        actionForm.ServiceGroupId,
                        actionForm.TopicPattern,
                        action.Idempotent?.Value.Value ?? false,
                        normalResultNames,
                        normalRequiredNames,
                        outArgsType,
                        errRespName,
                        errSchemaName,
                        actionForm.HeaderCodeName,
                        headerCodeSchema,
                        actionForm.HeaderInfoName,
                        headerInfoSchema,
                        actionForm.HeaderCodeSchema?.Value.Enum?.Elements?.Select(e => e.Value.Value).ToList(),
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
                            actionForm.ErrorRespSchema.Value.Description?.Value.Value ?? "The action could not be completed",
                            actionForm.ErrorRespSchema.Value.ErrorMessage?.Value.Value,
                            actionForm.ErrorRespSchema.Value.Required?.Elements?.Any(e => e.Value.Value == (actionForm.ErrorRespSchema.Value.ErrorMessage?.Value?.Value ?? string.Empty)) ?? false,
                            actionForm.HeaderCodeName,
                            schemaNamer.ChooseTitleOrName(actionForm.HeaderCodeSchema?.Value.Title?.Value.Value, actionForm.HeaderCodeName),
                            actionForm.HeaderInfoName,
                            schemaNamer.ChooseTitleOrName(actionForm.HeaderInfoSchema?.Value.Title?.Value.Value, actionForm.HeaderInfoName));

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
