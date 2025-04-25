// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using System.Text.Json;
using System.Text;
using System.Data.SqlClient;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;
using Azure.Iot.Operations.Connector.Assets;

namespace SqlQualityAnalyzerConnectorApp
{
    internal class QualityAnalyzerDatasetSampler : IDatasetSampler
    {
        private readonly string _connectionString;
        private string _fullConnectionString = "";
        private readonly string _assetName;
        private readonly DeviceCredentials? _credentials;

        public QualityAnalyzerDatasetSampler(string connectionString, string assetName, DeviceCredentials? deviceCredentials)
        {
            _connectionString = connectionString;
            _assetName = assetName;
            _credentials = deviceCredentials;
        }

        public async Task<byte[]> SampleDatasetAsync(AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            try
            {
                AssetDatasetDataPointSchemaElement sqlServerCountryDataPoint = dataset.DataPointsDictionary!["Country"];
                string sqlServerCountryTable = sqlServerCountryDataPoint.DataSource!;
                AssetDatasetDataPointSchemaElement sqlServerViscosityDataPoint = dataset.DataPointsDictionary!["Viscosity"];
                AssetDatasetDataPointSchemaElement sqlServerSweetnessDataPoint = dataset.DataPointsDictionary!["Sweetness"];
                AssetDatasetDataPointSchemaElement sqlServerParticleSizeDataPoint = dataset.DataPointsDictionary!["ParticleSize"];
                AssetDatasetDataPointSchemaElement sqlServerOverallDataPoint = dataset.DataPointsDictionary!["Overall"];

                string query = $"SELECT {sqlServerCountryDataPoint.Name}, {sqlServerViscosityDataPoint.Name}, {sqlServerSweetnessDataPoint.Name}, {sqlServerParticleSizeDataPoint.Name}, {sqlServerOverallDataPoint.Name} from CountryMeasurements";

                if (_credentials != null && _credentials.Username != null && _credentials.Password != null)
                {
                    // Note that this sample uses username + password for authenticating the connection to the asset. In general,
                    // x509 authentication should be used instead (if available) as it is more secure.
                    string sqlServerUsername = _credentials.Username;
                    byte[] sqlServerPassword = _credentials.Password;
                    _fullConnectionString = _connectionString + $"User Id={sqlServerUsername};Password={Encoding.UTF8.GetString(sqlServerPassword)};TrustServerCertificate=true;";
                }

                // In this sample, the datapoints have the different datasource, there are 2 options to get the data

                // Option 1: Get the data joining tables
                // Option 2: Get the data from each table by doing multiple queries and join them in the code
                List<QualityAnalyzerData> qualityAnalyzerDataList = new List<QualityAnalyzerData>();
                using (SqlConnection connection = new SqlConnection(_fullConnectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.HasRows)
                            {
                                while (await reader.ReadAsync())
                                {
                                    QualityAnalyzerData analyzerData = new QualityAnalyzerData
                                    {
                                        Viscosity = double.Parse(reader["Viscosity"]?.ToString() ?? "0.0"),
                                        Sweetness = double.Parse(reader["Sweetness"]?.ToString() ?? "0.0"),
                                        ParticleSize = double.Parse(reader["ParticleSize"]?.ToString() ?? "0.0"),
                                        Overall = double.Parse(reader["Overall"]?.ToString() ?? "0.0"),
                                        Country = reader["Country"]?.ToString()
                                    };
                                    qualityAnalyzerDataList.Add(analyzerData);
                                }
                            }
                        }
                    }
                }
                return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(qualityAnalyzerDataList));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to sample dataset with name {dataset.Name} in asset with name {_assetName}", ex);
            }
        }

        public Task<TimeSpan> GetSamplingIntervalAsync(AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(TimeSpan.FromSeconds(1));
        }

        public ValueTask DisposeAsync()
        {
            // Nothing to dispose yet
            return ValueTask.CompletedTask;
        }
    }
}
