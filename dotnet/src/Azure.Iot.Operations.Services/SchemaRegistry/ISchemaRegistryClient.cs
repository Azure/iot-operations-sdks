namespace Azure.Iot.Operations.Services.SchemaRegistry;

using SchemaInfo = dtmi_ms_adr_SchemaRegistry__1.Object_Ms_Adr_SchemaRegistry_Schema__1;
using SchemaFormat = dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_Format__1;
using SchemaType = dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_SchemaType__1;

public interface ISchemaRegistryClient : IAsyncDisposable
{
    public Task<SchemaInfo> GetAsync(string schemaId, string version = "1.0.0", TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
    public Task<SchemaInfo> PutAsync(string schemaContent, SchemaFormat schemaFormat, SchemaType schemaType = SchemaType.MessageSchema, string version = "1.0.0", Dictionary<string, string> tags = default!, TimeSpan? timeout = default!, CancellationToken cancellationToken = default!);
}
