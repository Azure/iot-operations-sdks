using Azure.Iot.Operations.Services.Assets;
using Azure.Iot.Operations.Services.SchemaRegistry.dtmi_ms_adr_SchemaRegistry__1;
using System.Text;

namespace Azure.Iot.Operations.Connector.UnitTests
{
    internal class DatasetSampler : IDatasetSampler
    {
        public Task<Object_Ms_Adr_SchemaRegistry_Schema__1> GetMessageSchemaAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Encoding.UTF8.GetBytes("someData"));
        }
    }
}
