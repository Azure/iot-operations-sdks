using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Protocol.Models
{
    public interface IMqttClientChannelOptions
    {
        MqttClientTlsOptions TlsOptions { get; }
    }
}
