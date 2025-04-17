﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record TrustSettings
{
    public string? IssuerList { get; set; }

    public string? TrustList { get; set; }

    public string TrustMode { get; set; } = default!;
}
