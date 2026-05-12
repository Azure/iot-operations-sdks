// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Services.EdgeRegistry.Generated;

namespace Azure.Iot.Operations.Services.EdgeRegistry.Models;

internal static class Converter
{
    internal static ServiceError ToServiceError(EdgeRegistryError error)
    {
        return new ServiceError
        {
            Code = error.Code,
            Detail = error.Detail,
            Instance = error.Instance,
            Status = error.Status,
            Title = error.Title,
            TypeUri = error.Type,
        };
    }

    internal static ServiceError ToServiceError(SchemaExtensionError error)
    {
        return new ServiceError
        {
            Code = error.Code,
            Detail = error.Detail,
            Instance = error.Instance,
            Status = error.Status,
            Title = error.Title,
            TypeUri = error.Type,
        };
    }

    internal static ServiceError ToServiceError(ThingDescriptionExtensionError error)
    {
        return new ServiceError
        {
            Code = error.Code,
            Detail = error.Detail,
            Instance = error.Instance,
            Status = error.Status,
            Title = error.Title,
            TypeUri = error.Type,
        };
    }
}
