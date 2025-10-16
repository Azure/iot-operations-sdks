namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;

    public record AggregateErrorSpec(string SchemaName, Dictionary<string, string> InnerErrors);
}
