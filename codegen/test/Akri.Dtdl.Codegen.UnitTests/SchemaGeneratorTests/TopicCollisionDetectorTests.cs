namespace Akri.Dtdl.Codegen.UnitTests.SchemaGeneratorTests
{
    using DTDLParser;
    using DTDLParser.Models;
    using Akri.Dtdl.Codegen;

    public class TopicCollisionDetectorTests
    {
        private const string rootPath = "../../../SchemaGeneratorTests";
        private const string modelsPath = $"{rootPath}/models";
        private const string twoCommandModelPath = $"{modelsPath}/TwoCommands.json";
        private const string twoTelemetryModelPath = $"{modelsPath}/TwoTelemetries.json";
        private const string oneCommandModelPath = $"{modelsPath}/OneCommand.json";
        private const string oneTelemetryModelPath = $"{modelsPath}/OneTelemetry.json";

        private static readonly Dtmi interfaceIdX = new Dtmi("dtmi:akri:dtdl:testInterfaceX;1");
        private static readonly Dtmi interfaceIdY = new Dtmi("dtmi:akri:dtdl:testInterfaceY;1");

        private readonly ModelParser modelParser;

        private readonly string twoCommandModelTemplate;
        private readonly string twoTelemetryModelTemplate;
        private readonly string oneCommandModelTemplate;
        private readonly string oneTelemetryModelTemplate;

        public TopicCollisionDetectorTests()
        {
            modelParser = new ModelParser();

            twoCommandModelTemplate = File.OpenText(twoCommandModelPath).ReadToEnd();
            twoTelemetryModelTemplate = File.OpenText(twoTelemetryModelPath).ReadToEnd();
            oneCommandModelTemplate = File.OpenText(oneCommandModelPath).ReadToEnd();
            oneTelemetryModelTemplate = File.OpenText(oneTelemetryModelPath).ReadToEnd();
        }

        [Theory]
        [InlineData("Foo", "Bar", "Baz", "Qux", "akri/dtdl/{modelId}/{executorId}/{commandName}", false)]
        [InlineData("Foo", "Bar", "Bar", "Baz", "akri/dtdl/{modelId}/{executorId}/{commandName}", false)]
        [InlineData("Foo", "Bar", "Baz", "Qux", "akri/dtdl/{executorId}/{commandName}", false)]
        [InlineData("Foo", "Bar", "Bar", "Baz", "akri/dtdl/{executorId}/{commandName}", true)]
        public void TestInterInterfaceCommandCollisions(string nameX1, string nameX2, string nameY1, string nameY2, string topic, bool collides)
        {
            TopicCollisionDetector commandTopicCollisionDetector = TopicCollisionDetector.GetCommandTopicCollisionDetector();

            var modelDict = modelParser.Parse(new string[]
            {
                twoCommandModelTemplate.Replace("<[ID]>", interfaceIdX.AbsoluteUri).Replace("<[TOPIC]>", topic).Replace("<[NAME1]>", nameX1).Replace("<[NAME2]>", nameX2),
                twoCommandModelTemplate.Replace("<[ID]>", interfaceIdY.AbsoluteUri).Replace("<[TOPIC]>", topic).Replace("<[NAME1]>", nameY1).Replace("<[NAME2]>", nameY2),
            });

            DTInterfaceInfo dtInterfaceX = (DTInterfaceInfo)modelDict[interfaceIdX];
            DTInterfaceInfo dtInterfaceY = (DTInterfaceInfo)modelDict[interfaceIdY];

            commandTopicCollisionDetector.Check(dtInterfaceX, dtInterfaceX.Commands.Keys);

            bool collisionDetected = false;
            try
            {
                commandTopicCollisionDetector.Check(dtInterfaceY, dtInterfaceY.Commands.Keys);
            }
            catch
            {
                collisionDetected = true;
            }

            Assert.Equal(collides, collisionDetected);
        }

        [Theory]
        [InlineData("Foo", "Bar", "Baz", "Qux", "akri/dtdl/{modelId}/{senderId}/{telemetryName}", false)]
        [InlineData("Foo", "Bar", "Bar", "Baz", "akri/dtdl/{modelId}/{senderId}/{telemetryName}", false)]
        [InlineData("Foo", "Bar", "Baz", "Qux", "akri/dtdl/{senderId}/{telemetryName}", false)]
        [InlineData("Foo", "Bar", "Bar", "Baz", "akri/dtdl/{senderId}/{telemetryName}", true)]
        [InlineData("Foo", "Bar", "Baz", "Qux", "akri/dtdl/{modelId}/{senderId}", false)]
        [InlineData("Foo", "Bar", "Bar", "Baz", "akri/dtdl/{modelId}/{senderId}", false)]
        [InlineData("Foo", "Bar", "Baz", "Qux", "akri/dtdl/{senderId}", true)]
        [InlineData("Foo", "Bar", "Bar", "Baz", "akri/dtdl/{senderId}", true)]
        public void TestInterInterfaceTelemetryCollisions(string nameX1, string nameX2, string nameY1, string nameY2, string topic, bool collides)
        {
            TopicCollisionDetector telemetryTopicCollisionDetector = TopicCollisionDetector.GetTelemetryTopicCollisionDetector();

            var modelDict = modelParser.Parse(new string[]
            {
                twoTelemetryModelTemplate.Replace("<[ID]>", interfaceIdX.AbsoluteUri).Replace("<[TOPIC]>", topic).Replace("<[NAME1]>", nameX1).Replace("<[NAME2]>", nameX2),
                twoTelemetryModelTemplate.Replace("<[ID]>", interfaceIdY.AbsoluteUri).Replace("<[TOPIC]>", topic).Replace("<[NAME1]>", nameY1).Replace("<[NAME2]>", nameY2),
            });

            DTInterfaceInfo dtInterfaceX = (DTInterfaceInfo)modelDict[interfaceIdX];
            DTInterfaceInfo dtInterfaceY = (DTInterfaceInfo)modelDict[interfaceIdY];

            telemetryTopicCollisionDetector.Check(dtInterfaceX, dtInterfaceX.Telemetries.Keys);

            bool collisionDetected = false;
            try
            {
                telemetryTopicCollisionDetector.Check(dtInterfaceY, dtInterfaceY.Telemetries.Keys);
            }
            catch
            {
                collisionDetected = true;
            }

            Assert.Equal(collides, collisionDetected);
        }

        [Theory]
        [InlineData(1, "akri/dtdl/{modelId}/{executorId}/{commandName}", false)]
        [InlineData(2, "akri/dtdl/{modelId}/{executorId}/{commandName}", false)]
        [InlineData(1, "akri/dtdl/{modelId}/{executorId}", false)]
        [InlineData(2, "akri/dtdl/{modelId}/{executorId}", true)]
        public void TestIntraInterfaceCommandCollisions(int commandCount, string topic, bool collides)
        {
            TopicCollisionDetector commandTopicCollisionDetector = TopicCollisionDetector.GetCommandTopicCollisionDetector();

            string commandModelTemplate = commandCount switch
            {
                1 => oneCommandModelTemplate,
                2 => twoCommandModelTemplate,
                _ => throw new Exception("INTERNAL TEST FAILURE"),
            };
                
            var modelDict = modelParser.Parse(commandModelTemplate.Replace("<[ID]>", interfaceIdX.AbsoluteUri).Replace("<[TOPIC]>", topic).Replace("<[NAME1]>", "Foo").Replace("<[NAME2]>", "Bar"));

            DTInterfaceInfo dtInterfaceX = (DTInterfaceInfo)modelDict[interfaceIdX];

            bool collisionDetected = false;
            try
            {
                commandTopicCollisionDetector.Check(dtInterfaceX, dtInterfaceX.Commands.Keys);
            }
            catch
            {
                collisionDetected = true;
            }

            Assert.Equal(collides, collisionDetected);
        }

        [Theory]
        [InlineData(1, "akri/dtdl/{modelId}/{senderId}/{telemetryName}", false)]
        [InlineData(2, "akri/dtdl/{modelId}/{senderId}/{telemetryName}", false)]
        [InlineData(1, "akri/dtdl/{modelId}/{senderId}", false)]
        [InlineData(2, "akri/dtdl/{modelId}/{senderId}", false)]
        public void TestIntraInterfaceTelemetryCollisions(int telemetryCount, string topic, bool collides)
        {
            TopicCollisionDetector telemetryTopicCollisionDetector = TopicCollisionDetector.GetTelemetryTopicCollisionDetector();

            string telemetryModelTemplate = telemetryCount switch
            {
                1 => oneTelemetryModelTemplate,
                2 => twoTelemetryModelTemplate,
                _ => throw new Exception("INTERNAL TEST FAILURE"),
            };

            var modelDict = modelParser.Parse(telemetryModelTemplate.Replace("<[ID]>", interfaceIdX.AbsoluteUri).Replace("<[TOPIC]>", topic).Replace("<[NAME1]>", "Foo").Replace("<[NAME2]>", "Bar"));

            DTInterfaceInfo dtInterfaceX = (DTInterfaceInfo)modelDict[interfaceIdX];

            bool collisionDetected = false;
            try
            {
                telemetryTopicCollisionDetector.Check(dtInterfaceX, dtInterfaceX.Telemetries.Keys);
            }
            catch
            {
                collisionDetected = true;
            }

            Assert.Equal(collides, collisionDetected);
        }
    }
}
