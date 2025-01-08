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
        private string _connectionString;
        private string _assetName;
        private AssetEndpointProfileCredentials _credentials;

        public QualityAnalyzerDatasetSampler(string connectionString, string assetName, AssetEndpointProfileCredentials credentials)
        {
            _connectionString = connectionString;
            _assetName = assetName;
            _credentials = credentials;

        }

        public async Task<byte[]> SampleDatasetAsync(Dataset dataset, CancellationToken cancellationToken = default)
        {
            try
            {
                DataPoint sqlServerCountryDataPoint = dataset.DataPointsDictionary!["Country"];
                string sqlServerCountryTable = sqlServerCountryDataPoint.DataSource!;
                DataPoint sqlServerViscosityDataPoint = dataset.DataPointsDictionary!["Viscosity"];
                //string sqlServerViscosityTable = sqlServerViscosityDataPoint.DataSource!;
                DataPoint sqlServerSweetnessDataPoint = dataset.DataPointsDictionary!["Sweetness"];
                //string sqlServerSweetnessTable = sqlServerSweetnessDataPoint.DataSource!;
                DataPoint sqlServerParticleSizeDataPoint = dataset.DataPointsDictionary!["ParticleSize"];
                //string sqlServerParticleSizeTable = sqlServerParticleSizeDataPoint.DataSource!;
                DataPoint sqlServerOverallDataPoint = dataset.DataPointsDictionary!["Overall"];
                //string sqlServerOverallTable = sqlServerOverallDataPoint.DataSource!;
                string query = $"SELECT {sqlServerCountryDataPoint.Name}, {sqlServerViscosityDataPoint.Name}, {sqlServerSweetnessDataPoint.Name}, {sqlServerParticleSizeDataPoint.Name}, {sqlServerOverallDataPoint.Name} from CountryMeasurements";

                //DataPoint sqlServerSweetnessDataPoint = dataset.DataPointsDictionary!["Speed"];
                //string sqlServerSweetnessTable = sqlServerSweetnessDataPoint.DataSource!;
                //string windSpeedQuery = $"SELECT {sqlServerSweetnessDataPoint.Name} FROM {sqlServerSweetnessTable}";

                //DataPoint sqlServerGustDataPoint = dataset.DataPointsDictionary!["Duration"];
                //string sqlServerGustTable = sqlServerGustDataPoint.DataSource!;
                //string gustSpeedQuery = $"SELECT {sqlServerGustDataPoint.Name} FROM {sqlServerGustTable}";

                //DataPoint sqlServerDirectionDataPoint = dataset.DataPointsDictionary!["Direction"];
                //string sqlServerDirectionTable = sqlServerDirectionDataPoint.DataSource!;
                //string directionQuery = $"SELECT {sqlServerDirectionDataPoint.Name} FROM {sqlServerDirectionTable}";

                //string query = $"SELECT " +
                //    $"{sqlServerViscosityTable}.{sqlServerViscosityDataPoint.Name}, " +
                //    $"{sqlServerGustTable}.{sqlServerGustDataPoint.Name}, " +
                //    $"{sqlServerDirectionTable}.{sqlServerDirectionDataPoint.Name} " +
                //    $"FROM {sqlServerViscosityTable} " +
                //    $"JOIN  {sqlServerDirectionTable} ON {sqlServerViscosityTable}.Location =  {sqlServerDirectionTable}.Location " +
                //    $"JOIN {sqlServerGustTable} ON {sqlServerViscosityTable}.Location = {sqlServerGustTable}.Location;";

                if (_credentials != null)
                {
                    string sqlServerUsername = _credentials.Username!;
                    byte[] sqlServerPassword = _credentials.Password!;
                    Console.WriteLine($"Username: {sqlServerUsername}");
                    //Console.WriteLine($"Password: {Encoding.UTF8.GetString(sqlServerUsername)}");
                    Console.WriteLine($"Username: {sqlServerPassword}");
                    Console.WriteLine($"Password: {Encoding.UTF8.GetString(sqlServerPassword)}");
                    _connectionString = _connectionString + $"User Id={sqlServerUsername};Password={sqlServerPassword};";
                    // var byteArray = Encoding.ASCII.GetBytes($"{sqlServerUsername}:{Encoding.UTF8.GetString(sqlServerPassword)}");
                }

                // In this sample, the datapoints have the different datasource, there are 2 options to get the data

                // Option 1: Get the data joining tables
                // Option 2: Get the data from each table by doing multiple queries and join them in the code
                List<QualityAnalyzerData> qualityAnalyzerDataList = new List<QualityAnalyzerData>();
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    Console.WriteLine("Using sql connection");
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
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
