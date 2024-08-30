namespace Azure.Iot.Operations.Protocol.UnitTests.Protocol
{
    public class TestCaseError
    {
        public TestErrorKind Kind { get; set; } = TestErrorKind.None;

        public string? Message { get; set; }

        public string? PropertyName { get; set; }

        public string? PropertyValue { get; set; }
    }
}
