namespace Azure.Iot.Operations.ProtocolCompiler.UnitTests.RecursionCheckerTests
{
    using Azure.Iot.Operations.ProtocolCompilerLib;

    public class RecursionCheckerTests
    {
        [Fact]
        public void NoLoopDetectedInLoopFreeGraph()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.False(recursionChecker.TryDetectLoop(GetObject("alpha", "beta", "gamma"), out _));
            Assert.False(recursionChecker.TryDetectLoop(GetObject("beta", "gamma", "epsilon"), out _));
            Assert.False(recursionChecker.TryDetectLoop(GetObject("gamma", "delta", "epsilon"), out _));
        }

        [Fact]
        public void SingleEdgeLoopDetected()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.True(recursionChecker.TryDetectLoop(GetObject("alpha", "beta", "alpha"), out CodeName? name));
            Assert.Equal(new CodeName("alpha"), name);
        }

        [Fact]
        public void IndirectSingleEdgeLoopNotDetected()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.False(recursionChecker.TryDetectLoop(GetObject("alpha", "beta", "*alpha"), out _));
        }

        [Fact]
        public void DoubleEdgeLoopDetected()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.False(recursionChecker.TryDetectLoop(GetObject("alpha", "beta", "gamma"), out _));
            Assert.True(recursionChecker.TryDetectLoop(GetObject("beta", "alpha"), out CodeName? name));
            Assert.Equal(new CodeName("beta"), name);
        }

        [Fact]
        public void IndirectDoubleEdgeLoopNotDetected1()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.False(recursionChecker.TryDetectLoop(GetObject("alpha", "*beta", "gamma"), out _));
            Assert.False(recursionChecker.TryDetectLoop(GetObject("beta", "alpha"), out _));
        }

        [Fact]
        public void IndirectDoubleEdgeLoopNotDetected2()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.False(recursionChecker.TryDetectLoop(GetObject("alpha", "beta", "gamma"), out _));
            Assert.False(recursionChecker.TryDetectLoop(GetObject("beta", "*alpha"), out _));
        }

        [Fact]
        public void LatentDoubleEdgeLoopDetected()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.False(recursionChecker.TryDetectLoop(GetObject("alpha", "beta", "gamma"), out _));
            Assert.False(recursionChecker.TryDetectLoop(GetObject("beta", "gamma", "epsilon"), out _));
            Assert.True(recursionChecker.TryDetectLoop(GetObject("gamma", "alpha"), out CodeName? name));
            Assert.Equal(new CodeName("gamma"), name);
        }

        [Fact]
        public void TripleEdgeLoopDetected()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.False(recursionChecker.TryDetectLoop(GetObject("alpha", "beta", "gamma"), out _));
            Assert.False(recursionChecker.TryDetectLoop(GetObject("beta", "gamma", "epsilon"), out _));
            Assert.True(recursionChecker.TryDetectLoop(GetObject("epsilon", "alpha"), out CodeName? name));
            Assert.Equal(new CodeName("epsilon"), name);
        }

        [Fact]
        public void IndirectTripleEdgeLoopNotDetected()
        {
            RecursionChecker recursionChecker = new RecursionChecker();

            Assert.False(recursionChecker.TryDetectLoop(GetObject("alpha", "beta", "gamma"), out _));
            Assert.False(recursionChecker.TryDetectLoop(GetObject("beta", "*gamma", "*epsilon"), out _));
            Assert.False(recursionChecker.TryDetectLoop(GetObject("epsilon", "alpha"), out _));
        }

        private ObjectType GetObject(string schema, string field1 = "", string field2 = "", string field3 = "")
        {
            Dictionary<CodeName, ObjectType.FieldInfo> fieldInfos = new ();
            if (field1 != "")
            {
                fieldInfos.Add(new CodeName("field1"), GetField(field1));
            }
            if (field2 != "")
            {
                fieldInfos.Add(new CodeName("field2"), GetField(field2));
            }
            if (field3 != "")
            {
                fieldInfos.Add(new CodeName("field3"), GetField(field3));
            }
            return new ObjectType(new CodeName(schema), new CodeName(), string.Empty, fieldInfos);
        }

        private ObjectType.FieldInfo GetField(string fieldString)
        {
            bool isIndirect = fieldString.StartsWith('*');
            string fieldSchema = isIndirect ? fieldString.Substring(1) : fieldString;
            return new ObjectType.FieldInfo(new ReferenceType(new CodeName(fieldSchema), new CodeName()), isIndirect: isIndirect, isRequired: false, string.Empty, null);
        }
    }
}
