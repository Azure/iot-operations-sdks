// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License

namespace Azure.Iot.Operations.Opc2WotLib
{
    using System.Collections.Generic;

    public record UaVariableRecord(OpcUaVariable UaVariable, string? ContainedIn, List<string> Contains, bool IsDataVariable);
}
