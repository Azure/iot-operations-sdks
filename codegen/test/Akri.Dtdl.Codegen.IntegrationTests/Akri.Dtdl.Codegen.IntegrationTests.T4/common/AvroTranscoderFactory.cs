
namespace Akri.Dtdl.Codegen.IntegrationTests.T4
{
    public class AvroTranscoderFactory : ITranscoderFactory
    {
        public IDotnetTranscoder GetDotnetTranscoder()
        {
            return new AvroDotnetTranscoder();
        }
    }
}
