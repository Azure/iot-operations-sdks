// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

/// <summary>
/// Exception thrown when an Edge Registry service operation fails.
/// </summary>
public class EdgeRegistryServiceException : Exception
{
    public EdgeRegistryServiceException(ServiceError serviceError)
        : base($"{serviceError.Code} {serviceError.Status}. {serviceError.Title}")
    {
        ServiceError = serviceError;
    }

    public EdgeRegistryServiceException(ServiceError serviceError, Exception innerException)
        : base($"{serviceError.Code} {serviceError.Status}. {serviceError.Title}", innerException)
    {
        ServiceError = serviceError;
    }

    /// <summary>
    /// The service error details.
    /// </summary>
    public ServiceError ServiceError { get; }
}
