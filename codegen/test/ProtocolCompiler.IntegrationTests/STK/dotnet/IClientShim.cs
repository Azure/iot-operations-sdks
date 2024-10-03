namespace Azure.Iot.Operations.ProtocolCompiler.IntegrationTests.STK
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;
    using Azure.Iot.Operations.Protocol.RPC;

    public interface IClientShim
    {
        void ClearHandlers();

        void RegisterHandler(string telemetryName, Action<string, JToken> handler);

        Task StartAsync(CancellationToken cancellationToken = default);

        Task<JToken> InvokeCommand(string commandName, JToken? requestToken, TimeSpan commandTimeout);

        RpcCallAsync<JToken> InvokeCommand(string commandName, JToken? requestToken, CommandRequestMetadata requestMetadata, TimeSpan commandTimeout);
    }
}
