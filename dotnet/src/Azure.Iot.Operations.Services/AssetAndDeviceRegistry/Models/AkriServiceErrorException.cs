namespace Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models
{
    using System;
    using Azure.Iot.Operations.Services.AssetAndDeviceRegistry;

    public partial class AkriServiceErrorException : Exception
    {
        public AkriServiceErrorException(AkriServiceError akriServiceError)
            : base(akriServiceError.Message)
        {
            AkriServiceError = akriServiceError;
        }

        public AkriServiceError AkriServiceError { get; }
    }
}
