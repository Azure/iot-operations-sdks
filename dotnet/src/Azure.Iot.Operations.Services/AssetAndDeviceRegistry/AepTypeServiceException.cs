// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AepTypeServiceException : Exception
{
    public object? PropertyValue { get; internal set; }

    public string? PropertyName { get; internal set; }

    public AepTypeServiceException(string? message, string? ePropertyName, object? ePropertyValue) : base(message)
    {
        PropertyValue = ePropertyValue;
        PropertyName = ePropertyName;
    }

    public AepTypeServiceException(string? message, Exception? innerException, string? propertyName, object? propertyValue) : base(message, innerException)
    {
        PropertyName = propertyName;
        PropertyValue = propertyValue;
    }

    public AepTypeServiceException(string? propertyName, object? propertyValue)
    {
        PropertyName = propertyName;
        PropertyValue = propertyValue;
    }
}
