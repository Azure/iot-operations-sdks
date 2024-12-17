using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Models
{
    public interface IMqttExtendedAuthenticationExchangeHandler
    {
        Task HandleRequestAsync(MqttExtendedAuthenticationExchangeContext context);
    }
}
