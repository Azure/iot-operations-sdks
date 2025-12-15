namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public record ConstantsSpec(string? Description, Dictionary<CodeName, TypedConstant> Constants);
}
