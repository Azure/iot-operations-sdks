// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record ConfigErrorDetails
{
    public string? Code { get; set; }

    public string? CorrelationId { get; set; }

    public string? Info { get; set; }

    public string? Message { get; set; }

    internal bool EqualTo(ConfigErrorDetails other)
    {
        if (!string.Equals(Code, other.Code))
        {
            return false;
        }

        if (!string.Equals(CorrelationId, other.CorrelationId))
        {
            return false;
        }

        if (!string.Equals(Info, other.Info))
        {
            return false;
        }

        if (!string.Equals(Message, other.Message))
        {
            return false;
        }

        return true;
    }
}
