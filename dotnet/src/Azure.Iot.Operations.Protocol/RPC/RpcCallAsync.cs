using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public readonly struct RpcCallAsync<TResp>(Task<ExtendedResponse<TResp>> task, Guid requestCorrelationData)
        where TResp : class
    {
        public Task<ExtendedResponse<TResp>> WithMetadata()
        {
            return ExtendedAsync;
        }

        public Task<ExtendedResponse<TResp>> ExtendedAsync { get; } = task;

        public Guid RequestCorrelationData { get; } = requestCorrelationData;

        public TaskAwaiter<TResp> GetAwaiter()
        {
            return ExtendedAsync
            .ContinueWith(
                (exTask) =>
                {
                    if (exTask.IsFaulted)
                    {
                        Debug.Assert(exTask.Exception?.InnerException != null);
                        ExceptionDispatchInfo.Capture(exTask.Exception?.InnerException!).Throw();
                    }
                    return exTask.Result.Response;
                }).GetAwaiter();
        }
    }
}
