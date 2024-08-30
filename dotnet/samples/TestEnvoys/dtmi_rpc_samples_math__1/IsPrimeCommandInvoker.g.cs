/* This is an auto-generated file.  Do not modify. */

#nullable enable

namespace TestEnvoys.dtmi_rpc_samples_math__1
{
    using System;
    using System.Collections.Generic;
    using Azure.Iot.Operations.Protocol;
    using Azure.Iot.Operations.Protocol.RPC;
    using Azure.Iot.Operations.Protocol.Models;
    using TestEnvoys;

    public static partial class Math
    {
        /// <summary>
        /// Specializes the <c>CommandInvoker</c> class for Command 'isPrime'.
        /// </summary>
        public class IsPrimeCommandInvoker : CommandInvoker<IsPrimeCommandRequest, IsPrimeCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="IsPrimeCommandInvoker"/> class.
            /// </summary>
            internal IsPrimeCommandInvoker(IMqttPubSubClient mqttClient)
                : base(mqttClient, "isPrime", new ProtobufSerializer<IsPrimeCommandRequest, IsPrimeCommandResponse>())
            {
            }
        }
    }
}
