﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using MQTTnet.Diagnostics;
using MQTTnet.Diagnostics.Logger;
using System.Diagnostics;

namespace Azure.Iot.Operations.Mqtt;

public class MqttNetTraceLogger
{
    [DebuggerStepThrough()]
    public static MqttNetEventLogger CreateTraceLogger()
    {
        MqttNetEventLogger logger = new();
        logger.LogMessagePublished += (s, e) =>
        {
            string trace = $">> [{e.LogMessage.Timestamp:O}] [{e.LogMessage.ThreadId}]: {e.LogMessage.Message}";
            if (e.LogMessage.Exception != null)
            {
                trace += Environment.NewLine + e.LogMessage.Exception.ToString();
            }
            Trace.TraceInformation(trace);
        };
        return logger;
    }
}
