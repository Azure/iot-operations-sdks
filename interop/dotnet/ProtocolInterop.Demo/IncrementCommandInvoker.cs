namespace ProtocolInterop.Demo
{
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using ProtocolInterop;

    public static partial class CounterGroup
    {
        private static readonly JsonSerializerOptions JsonSerializerOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'increment'.
        /// </summary>
        public class IncrementCommandInvoker : Invoker
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="IncrementCommandInvoker"/> class.
            /// </summary>
            public IncrementCommandInvoker()
            {
            }

            public async Task<IncrementResponseSchema> InvokeCommandAsync(IncrementRequestPayload request)
            {
                string reqString = JsonSerializer.Serialize(request, JsonSerializerOptions);
                string respString = await Invoke(reqString);
                return JsonSerializer.Deserialize<IncrementResponseSchema>(respString, JsonSerializerOptions) !;
            }
        }
    }
}
