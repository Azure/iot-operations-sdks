namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public record AggregateErrorSchemaInfo(CodeName Schema, Dictionary<string, CodeName> InnerErrors);
}
