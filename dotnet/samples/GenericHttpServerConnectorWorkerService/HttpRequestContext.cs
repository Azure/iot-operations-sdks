
namespace GenericHttpServerConnectorWorkerService
{
    public class HttpRequestContext
    {
        public string HttpServerAddress { get; init; }
        public HttpMethod HttpMethod { get; init; }
        public string HttpPath { get; init; }
        public string PropertyName { get; init; }
        public string? HttpServerUsername { get; init; }
        public byte[]? HttpServerPassword { get; init; }

        public HttpRequestContext(string httpServerAddress, HttpMethod httpMethod, string httpPath, string propertyName, string? httpServerUsername, byte[]? httpServerPassword)
        {
            HttpServerAddress = httpServerAddress;
            HttpMethod = httpMethod;
            HttpPath = httpPath;
            PropertyName = propertyName;
            HttpServerUsername = httpServerUsername;
            HttpServerPassword = httpServerPassword;
        }
    }
}
