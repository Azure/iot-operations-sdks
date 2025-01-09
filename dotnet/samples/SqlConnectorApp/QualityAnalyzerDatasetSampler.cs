using Azure.Iot.Operations.Connector;
using Azure.Iot.Operations.Services.Assets;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Data.SqlClient;

namespace SqlQualityAnalyzerConnectorApp
{
    internal class QualityAnalyzerDatasetSampler : IDatasetSampler
    {
        //private readonly ILogger<QualityAnalyzerDatasetSampler> _logger;
        private string _connectionString;
        private string fullConnectionString = "";
        private string _assetName;
        private AssetEndpointProfileCredentials _credentials;

        public QualityAnalyzerDatasetSampler(string connectionString, string assetName, AssetEndpointProfileCredentials credentials)
        {
            _connectionString = connectionString;
            _assetName = assetName;
            _credentials = credentials;
            //_logger = logger;
        }

        public async Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            // if (_logger.IsEnabled(LogLevel.Information))
            // {
            //     _logger.LogInformation("Logger is working at Information level");
            // }
            // if (_logger == null)
            // {
            //     Console.WriteLine("Logger is null"); // Fallback logging
            // }
            // _logger.LogError("Test error message"); // This should show up in error logs
            try
            {
                //_logger.LogInformation($"In sample data sync");
                Console.WriteLine("In sample data sync");
                DataPoint sqlServerCountryDataPoint = dataset.DataPointsDictionary!["Country"];
                string sqlServerCountryTable = sqlServerCountryDataPoint.DataSource!;
                DataPoint sqlServerViscosityDataPoint = dataset.DataPointsDictionary!["Viscosity"];
                DataPoint sqlServerSweetnessDataPoint = dataset.DataPointsDictionary!["Sweetness"];
                DataPoint sqlServerParticleSizeDataPoint = dataset.DataPointsDictionary!["ParticleSize"];
                DataPoint sqlServerOverallDataPoint = dataset.DataPointsDictionary!["Overall"];

                string query = $"SELECT {sqlServerCountryDataPoint.Name}, {sqlServerViscosityDataPoint.Name}, {sqlServerSweetnessDataPoint.Name}, {sqlServerParticleSizeDataPoint.Name}, {sqlServerOverallDataPoint.Name} from CountryMeasurements";
                //_logger.LogInformation($"Query: {query}");
                Console.WriteLine($"Query: {query}");

                if (_credentials != null)
                {
                    string sqlServerUsername = _credentials.Username!;
                    byte[] sqlServerPassword = _credentials.Password!;
                    // _logger.LogInformation($"Username: {sqlServerUsername}");
                    // _logger.LogInformation($"Password: {sqlServerPassword}");
                    // _logger.LogInformation($"Password: {Encoding.UTF8.GetString(sqlServerPassword)}");
                    Console.WriteLine($"Username: {sqlServerUsername}");
                    Console.WriteLine($"Password: {Encoding.UTF8.GetString(sqlServerPassword)}");
                    fullConnectionString = _connectionString + $"User Id={sqlServerUsername};Password={Encoding.UTF8.GetString(sqlServerPassword)};TrustServerCertificate=true;";
                    //_connectionString = _connectionString + $"User Id={sqlServerUsername};Password={sqlServerPassword};TrustServerCertificate=true;";
                    //_logger.LogInformation($"connectionString: {_connectionString}");
                    Console.WriteLine($"connectionString: {fullConnectionString}");
                    // var byteArray = Encoding.ASCII.GetBytes($"{sqlServerUsername}:{Encoding.UTF8.GetString(sqlServerPassword)}");
                }

                // In this sample, the datapoints have the different datasource, there are 2 options to get the data

                // Option 1: Get the data joining tables
                // Option 2: Get the data from each table by doing multiple queries and join them in the code
                List<QualityAnalyzerData> qualityAnalyzerDataList = new List<QualityAnalyzerData>();
                using (SqlConnection connection = new SqlConnection(fullConnectionString))
                {
                    // _logger.LogInformation("Using sql connection");
                    Console.WriteLine("Using sql connection");
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        Console.WriteLine("Executed query");
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (reader.HasRows)
                            {
                                while (await reader.ReadAsync())
                                {
                                    QualityAnalyzerData analyzerData = new QualityAnalyzerData();
                                    analyzerData.Viscosity = double.Parse(reader["Viscosity"]?.ToString() ?? "0.0");
                                    analyzerData.Sweetness = double.Parse(reader["Sweetness"]?.ToString() ?? "0.0");
                                    analyzerData.ParticleSize = double.Parse(reader["ParticleSize"]?.ToString() ?? "0.0");
                                    analyzerData.Overall = double.Parse(reader["Overall"]?.ToString() ?? "0.0");
                                    analyzerData.Country = reader["Country"]?.ToString();
                                    qualityAnalyzerDataList.Add(analyzerData);
                                    Console.WriteLine($"Viscosity : {analyzerData.Viscosity}");
                                    Console.WriteLine($"Sweetness : {analyzerData.Sweetness}");
                                    Console.WriteLine($"ParticleSize : {analyzerData.ParticleSize}");
                                    Console.WriteLine($"Overall : {analyzerData.Overall}");
                                    Console.WriteLine($"Country : {analyzerData.Country}");
                                }
                            }
                        }
                    }
                }
                return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(qualityAnalyzerDataList));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logger error: {ex}"); // Fallback logging
                throw new InvalidOperationException($"Failed to sample dataset with name {dataset.Name} in asset with name {_assetName}", ex);
            }
        }

        public Task<DatasetMessageSchema?> GetMessageSchemaAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            // By returning null, no message schema will be registered for telemetry sent for this dataset.
            return Task.FromResult((DatasetMessageSchema?)null);
        }

        public ValueTask DisposeAsync()
        {
            // Nothing to dispose yet
            return ValueTask.CompletedTask;
        }
    }
}
