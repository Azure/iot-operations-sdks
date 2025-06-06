// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

public class AkriServiceErrorException(AkriServiceError akriServiceError) : Exception(akriServiceError.Message)
{
    public AkriServiceError AkriServiceError { get; } = akriServiceError;
}
