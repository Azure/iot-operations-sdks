namespace Azure.Iot.Operations.Services.SchemaRegistry;

using Azure.Iot.Operations.Protocol;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using SchemaInfo = dtmi_ms_adr_SchemaRegistry__1.Object_Ms_Adr_SchemaRegistry_Schema__1;
using SchemaFormat = dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_Format__1;
using SchemaType = dtmi_ms_adr_SchemaRegistry__1.Enum_Ms_Adr_SchemaRegistry_SchemaType__1;

public class SchemaRegistryClient(IMqttPubSubClient pubSubClient) : ISchemaRegistryClient
{
    private static readonly TimeSpan s_DefaultCommandTimeout = TimeSpan.FromSeconds(10);
    private readonly SchemaRegistryClientStub _clientStub = new (pubSubClient);
    private bool _disposed;

    public async Task<SchemaInfo?> GetAsync(
        string schemaId,
        string version = "1.0.0",
        TimeSpan? timeout = default,
        CancellationToken cancellationToken = default!)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _clientStub.GetAsync(
            new GetCommandRequest()
            {
                GetSchemaRequest = new()
                {
                    Name = schemaId,
                    Version = version
                }
            }, null, timeout ?? s_DefaultCommandTimeout, cancellationToken)).Schema;
    }

    public async Task<SchemaInfo?> PutAsync(
        string schemaContent,
        SchemaFormat schemaFormat,
        SchemaType schemaType = SchemaType.MessageSchema,
        string version = "1.0.0", 
        Dictionary<string, string> tags = default!, 
        TimeSpan? timeout = default!, 
        CancellationToken cancellationToken = default!)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        return (await _clientStub.PutAsync(
            new PutCommandRequest()
            {
                PutSchemaRequest = new()
                {
                    Format = schemaFormat,
                    SchemaContent = schemaContent,
                    Version = version,
                    Tags = tags,
                    SchemaType = schemaType
                }
            }, null, timeout ?? s_DefaultCommandTimeout, cancellationToken)).Schema;
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _clientStub.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
        _disposed = true;
    }
}
