// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol.IntegrationTests;
      
public class FaultInjectionTestConstants
{
	public const string disconnectFaultName = "fault:disconnect";
	public const string rejectConnectFaultName = "fault:rejectconnect";
	public const string disconnectFaultDelayName = "fault:delay";
	public const string faultRequestIdName = "fault:requestid";	
}