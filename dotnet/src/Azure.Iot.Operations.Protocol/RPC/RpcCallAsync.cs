using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.RPC
{
    public struct RpcCallAsync<TResp>
        where TResp : class
    {
        public Task<ExtendedResponse<TResp>> WithMetadata() => ExtendedAsync;

        public Task<ExtendedResponse<TResp>> ExtendedAsync { get; }

        public Guid RequestCorrelationData { get; }

        public RpcCallAsync(Task<ExtendedResponse<TResp>> task, Guid requestCorrelationData)
        {
            ExtendedAsync = task;
            RequestCorrelationData = requestCorrelationData;
        }

        public TaskAwaiter<TResp> GetAwaiter() => ExtendedAsync
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
