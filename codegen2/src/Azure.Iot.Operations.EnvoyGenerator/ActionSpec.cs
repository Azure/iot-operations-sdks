namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;
    using Azure.Iot.Operations.TDParser;

    public record ActionSpec(
        CodeName Name,
        CodeName Invoker,
        CodeName Executor,
        ITypeName? RequestSchema,
        ITypeName? ResponseSchema,
        EmptyTypeName SerializerEmptyType,
        List<CodeName> NormalResultNames,
        List<CodeName> NormalRequiredNames,
        CodeName? NormalResultSchema,
        CodeName? ErrorResultName,
        CodeName? ErrorResultSchema,
        CodeName? ErrorCodeName,
        CodeName? ErrorCodeSchema,
        CodeName? ErrorInfoName,
        CodeName? ErrorInfoSchema,
        bool DoesTargetExecutor,
        bool ResponseNullable)
    {
        public ActionSpec(
            SchemaNamer schemaNamer,
            string actionName,
            string? inputSchemaType,
            string? outputSchemaType,
            SerializationFormat format,
            List<string> normalResultNames,
            List<ValueTracker<StringHolder>> normalRequiredNames,
            string? normalResultSchema,
            string? errorResultName,
            string? errorResultSchema,
            string? headerCodeName,
            string? headerCodeSchema,
            string? headerInfoName,
            string? headerInfoSchema,
            bool doesTargetExecutor)
            : this(
                new CodeName(actionName),
                new CodeName(schemaNamer.GetActionInvokerBinder(actionName)),
                new CodeName(schemaNamer.GetActionExecutorBinder(actionName)),
                inputSchemaType != null ? new CodeName(inputSchemaType) : null,
                outputSchemaType != null ? new CodeName(outputSchemaType) : null,
                format.GetEmptyTypeName(),
                normalResultNames.ConvertAll(name => new CodeName(name)),
                normalRequiredNames.ConvertAll(name => new CodeName(name.Value.Value)),
                normalResultSchema != null ? new CodeName(normalResultSchema) : null,
                errorResultName != null ? new CodeName(errorResultName) : null,
                errorResultSchema != null ? new CodeName(errorResultSchema) : null,
                headerCodeName != null ? new CodeName(headerCodeName) : null,
                headerCodeSchema != null ? new CodeName(headerCodeSchema) : null,
                headerInfoName != null ? new CodeName(headerInfoName) : null,
                headerInfoSchema != null ? new CodeName(headerInfoSchema) : null,
                doesTargetExecutor,
                ResponseNullable: false)
        {
        }
    }
}
