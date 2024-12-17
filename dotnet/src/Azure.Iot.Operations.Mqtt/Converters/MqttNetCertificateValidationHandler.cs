﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Mqtt.Converters
{
    internal class MqttNetCertificateValidationHandler
    {
        private Func<MqttClientCertificateValidationEventArgs, bool> _genericNetFunc;

        public MqttNetCertificateValidationHandler(Func<MqttClientCertificateValidationEventArgs, bool> genericFunc)
        {
            _genericNetFunc = genericFunc;
        }

        public bool HandleCertificateValidation(MQTTnet.Client.MqttClientCertificateValidationEventArgs args)
        {
            return _genericNetFunc.Invoke(new MqttClientCertificateValidationEventArgs(args.Certificate, args.Chain, args.SslPolicyErrors, MqttNetConverter.ToGeneric(args.ClientOptions)));
        }
    }
}
