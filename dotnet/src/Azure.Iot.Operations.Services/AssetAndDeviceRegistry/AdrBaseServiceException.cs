// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

public class AdrBaseServiceException : Exception
{
    public object? PropertyValue { get; internal set; }

    public string? PropertyName { get; internal set; }

    public AdrBaseServiceException(string? message, string? ePropertyName, object? ePropertyValue) : base(message)
    {
        PropertyValue = ePropertyValue;
        PropertyName = ePropertyName;
    }

    public AdrBaseServiceException(string? message, Exception? innerException, string? propertyName, object? propertyValue) : base(message, innerException)
    {
        PropertyName = propertyName;
        PropertyValue = propertyValue;
    }

    public AdrBaseServiceException(string? propertyName, object? propertyValue)
    {
        PropertyName = propertyName;
        PropertyValue = propertyValue;
    }
}
