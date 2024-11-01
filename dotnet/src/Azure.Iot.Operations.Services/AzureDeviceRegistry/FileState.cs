
namespace Azure.Iot.Operations.Services.AzureDeviceRegistry
{
    internal class FileState
    {
        internal byte[] MostRecentContentsHash { get; set; }

        internal DateTime MostRecentWrite { get; set; }

        internal FileState(byte[] mostRecentContentsHash, DateTime mostRecentWrite)
        {
            MostRecentContentsHash = mostRecentContentsHash;
            MostRecentWrite = mostRecentWrite;
        }
    }
}
