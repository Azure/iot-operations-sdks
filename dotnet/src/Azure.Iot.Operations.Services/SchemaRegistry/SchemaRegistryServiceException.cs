using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Iot.Operations.Services.SchemaRegistry
{
    public class SchemaRegistryServiceException : Exception
    {
        /// <summary>
        /// The name of a method argument or a property in a class, configuration file, or environment variable that is missing or has an invalid value
        /// </summary>
        public string? PropertyName { get; internal init; }

        /// <summary>
        /// The value of a method argument or a property in a class, configuration file, or environment variable that is invalid
        /// </summary>
        public object? PropertyValue { get; internal init; }

        public SchemaRegistryServiceException(string? propertyName, object? propertyValue)
        {
            PropertyName = propertyName;
            PropertyValue = propertyValue;
        }

        public SchemaRegistryServiceException(string? message, string? propertyName, object? propertyValue) : base(message)
        {
            PropertyName = propertyName;
            PropertyValue = propertyValue;
        }

        public SchemaRegistryServiceException(string? message, Exception? innerException, string? propertyName, object? propertyValue) : base(message, innerException)
        {
            PropertyName = propertyName;
            PropertyValue = propertyValue;
        }
    }
}
