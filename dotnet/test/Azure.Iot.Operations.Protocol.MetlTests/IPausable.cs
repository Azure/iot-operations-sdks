namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    using System.Threading.Tasks;

    public interface IPausable
    {
        bool HasFired { get; }

        Task<bool> TryPauseAsync();

        Task ResumeAsync();
    }
}
