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
        /// Specializes the <c>CommandInvoker</c> class for Command 'fib'.
        /// </summary>
        public class FibCommandInvoker : CommandInvoker<FibCommandRequest, FibCommandResponse>
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="FibCommandInvoker"/> class.
            /// </summary>
            internal FibCommandInvoker(IMqttPubSubClient mqttClient)
                : base(mqttClient, "fib", new ProtobufSerializer<FibCommandRequest, FibCommandResponse>())
            {
            }
        }
    }
}
