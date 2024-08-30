namespace Akri.Dtdl.Codegen.IntegrationTests.STK
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using Azure.Iot.Operations.Protocol.RPC;

    public interface IServiceShim
    {
        void ClearHandlers();

        void RegisterHandler(string commandName, Func<JToken, CommandRequestMetadata, ExtendedResponse<JToken>?> handler);

        Task StartAsync(CancellationToken cancellationToken = default);

        void SendTelemetry(string telemetryName, JToken telemetryToken);
    }
}
