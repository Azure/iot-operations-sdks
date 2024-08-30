
namespace Akri.Dtdl.Codegen.IntegrationTests.T4
{
    public class JsonTranscoderFactory : ITranscoderFactory
    {
        private string emptySchemaType;

        public JsonTranscoderFactory(string emptySchemaType)
        {
            this.emptySchemaType = emptySchemaType;
        }

        public IDotnetTranscoder GetDotnetTranscoder()
        {
            return new JsonDotnetTranscoder(emptySchemaType);
        }
    }
}
