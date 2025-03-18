namespace Azure.Iot.Operations.Protocol.Docgen
{
    public record TestCaseDescription
    {
        public TestCaseDescription(string condition, string expect)
        {
            Condition = condition;
            Expect = expect;
        }

        public string Condition { get; }

        public string Expect { get; }
    }
}
