// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    public record OpcUaReference(OpcUaNodeId ReferenceType, OpcUaNodeId Target, bool IsForward);
}
