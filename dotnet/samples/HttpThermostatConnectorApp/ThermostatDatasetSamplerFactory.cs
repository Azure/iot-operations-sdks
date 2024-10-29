﻿using Azure.Iot.Operations.ConnectorSample;
using Azure.Iot.Operations.GenericHttpConnectorSample;
using Azure.Iot.Operations.Services.AzureDeviceRegistry;
using System.Net.Http;

namespace HttpThermostatConnectorApp
{
    internal class ThermostatDatasetSamplerFactory : IDatasetSamplerFactory
    {
        public static Func<IServiceProvider, IDatasetSamplerFactory> ThermostatDatasetSamplerFactoryProvider = service =>
        {
            return new ThermostatDatasetSamplerFactory();
        };

        public IDatasetSampler ConstructSampler(AssetEndpointProfile assetEndpointProfile, Dataset dataset)
        {
            if (dataset.Name.Equals("status"))
            {
                var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri(assetEndpointProfile.TargetAddress),
                };

                return new ThermostatStatusDatasetSampler(httpClient);
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized dataset with name {dataset.Name}");
            }
        }
    }
}
