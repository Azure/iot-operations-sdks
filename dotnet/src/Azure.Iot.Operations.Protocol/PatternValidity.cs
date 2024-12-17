// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Protocol
{
    public enum PatternValidity
    {
        Valid = 0,
        InvalidPattern = 1,
        InvalidToken = 2,
        MissingReplacement = 3,
        InvalidResidentReplacement = 4,
        InvalidTransientReplacement = 5,
    }
}
