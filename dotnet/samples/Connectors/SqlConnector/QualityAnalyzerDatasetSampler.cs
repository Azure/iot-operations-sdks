﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Iot.Operations.Connector;
using System.Text.Json;
using System.Text;
using System.Data.SqlClient;
using Azure.Iot.Operations.Services.AssetAndDeviceRegistry.Models;

namespace SqlQualityAnalyzerConnectorApp
{
    internal class QualityAnalyzerDatasetSampler : IDatasetSampler
    {
        private readonly string _connectionString;
        private string _fullConnectionString = "";
        private readonly string _assetName;
        private readonly Authentication? _credentials;

        public QualityAnalyzerDatasetSampler(string connectionString, string assetName, Authentication? credentials)
        {
            _connectionString = connectionString;
            _assetName = assetName;
            _credentials = credentials;
        }

        public async Task<byte[]> SampleDatasetAsync(AssetDatasetSchemaElement dataset, CancellationToken cancellationToken = default)
        {
            try
            {
                AssetDataPointSchemaElement sqlServerCountryDataPoint = dataset.DataPointsDictionary!["Country"];
                string sqlServerCountryTable = sqlServerCountryDataPoint.DataSource!;
                AssetDataPointSchemaElement sqlServerViscosityDataPoint = dataset.DataPointsDictionary!["Viscosity"];
                AssetDataPointSchemaElement sqlServerSweetnessDataPoint = dataset.DataPointsDictionary!["Sweetness"];
                AssetDataPointSchemaElement sqlServerParticleSizeDataPoint = dataset.DataPointsDictionary!["ParticleSize"];
                AssetDataPointSchemaElement sqlServerOverallDataPoint = dataset.DataPointsDictionary!["Overall"];

                string query = $"SELECT {sqlServerCountryDataPoint.Name}, {sqlServerViscosityDataPoint.Name}, {sqlServerSweetnessDataPoint.Name}, {sqlServerParticleSizeDataPoint.Name}, {sqlServerOverallDataPoint.Name} from CountryMeasurements";

                if (_credentials != null)
                {
                    // Note that this sample uses username + password for authenticating the connection to the asset. In general,
                    // x509 authentication should be used instead (if available) as it is more secure.
                    string sqlServerUsername = _credentials!.UsernamePasswordCredentials.UsernameSecretName!; //TODO "secret name" now? How do we look up the value?
                    byte[] sqlServerPassword = Encoding.UTF8.GetBytes(_credentials!.UsernamePasswordCredentials.PasswordSecretName!);
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

        public ValueTask DisposeAsync()
        {
            // Nothing to dispose yet
            return ValueTask.CompletedTask;
        }
    }
}
