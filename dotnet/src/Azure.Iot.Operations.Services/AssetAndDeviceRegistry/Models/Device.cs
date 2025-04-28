// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public record Device
{
    public string Name { get; set; } = default!;

    public DeviceSpecification Specification { get; set; } = default!;

    /// <summary>
    /// The 'status' Field.
    /// </summary>
    public DeviceStatus? Status { get; set; }
}
