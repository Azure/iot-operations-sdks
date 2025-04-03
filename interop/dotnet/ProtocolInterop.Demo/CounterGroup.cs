namespace ProtocolInterop.Demo
{
    using System;
    using System.Threading.Tasks;

    public static partial class CounterGroup
    {
        public abstract partial class Client
        {
            private readonly IncrementCommandInvoker incrementCommandInvoker;

            public Client()
            {
                incrementCommandInvoker = new IncrementCommandInvoker();
            }

            public Task<IncrementResponsePayload> IncrementAsync(IncrementRequestPayload request)
            {
                return this.IncrementInt(request);
            }

            private async Task<IncrementResponsePayload> IncrementInt(IncrementRequestPayload request)
            {
                IncrementResponseSchema response = await this.incrementCommandInvoker.InvokeCommandAsync(request);
                if (response.IncrementError != null)
                {
                    throw new CounterErrorException(response.IncrementError);
                }
                else if (response.CounterValue == null)
                {
                    throw new InvalidOperationException("Command response has neither normal nor error payload content");
                }
                else
                {
                    return new IncrementResponsePayload { CounterValue = response.CounterValue.Value };
                }
            }
        }
    }
}
