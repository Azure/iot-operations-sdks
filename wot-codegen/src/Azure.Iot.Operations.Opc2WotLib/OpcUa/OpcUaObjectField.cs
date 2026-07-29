// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    public record OpcUaObjectField(OpcUaNode ContainingNode, OpcUaNodeId? DataType, string? SymbolicName, int ValueRank, bool IsOptional, string? Description);
}
