namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCaseEpilogue
    {
        public List<string> SubscribedTopics { get; set; } = new();

        public int? PublicationCount { get; set; }

        public List<TestCasePublishedMessage> PublishedMessages { get; set; } = new();

        public int? AcknowledgementCount {  get; set; }

        public int? ExecutionCount { get; set; }

        public Dictionary<int, int> ExecutionCounts { get; set; } = new();

        public TestCaseCatch? Catch { get; set; }
    }
}
