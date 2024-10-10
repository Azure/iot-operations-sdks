
namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.T4
{
    public class ProtobufTranscoderFactory : ITranscoderFactory
    {
        public IDotnetTranscoder GetDotnetTranscoder()
        {
            return new ProtobufDotnetTranscoder();
        }
    }
}
