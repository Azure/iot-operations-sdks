
namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public class AvroTranscoderFactory : ITranscoderFactory
    {
        public IDotnetTranscoder GetDotnetTranscoder()
        {
            return new AvroDotnetTranscoder();
        }
    }
}
