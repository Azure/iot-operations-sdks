
namespace Akri.Dtdl.Codegen.IntegrationTests.T4
{
    public class ProtobufTranscoderFactory : ITranscoderFactory
    {
        public IDotnetTranscoder GetDotnetTranscoder()
        {
            return new ProtobufDotnetTranscoder();
        }
    }
}
