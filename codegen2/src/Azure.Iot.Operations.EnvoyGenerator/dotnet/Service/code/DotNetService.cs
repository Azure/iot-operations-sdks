namespace Azure.Iot.Operations.EnvoyGenerator
{
    using System.Collections.Generic;
    using Azure.Iot.Operations.CodeGeneration;

    public partial class DotNetService : IEnvoyTemplateTransform
    {
        private readonly CodeName readRequesterName;
        private readonly CodeName writeRequesterName;
        private readonly CodeName readResponderName;
        private readonly CodeName writeResponderName;
        private readonly string projectName;
        private readonly CodeName genNamespace;
        private readonly CodeName serviceName;
        private readonly List<ActionSpec> actionSpecs;
        private readonly List<PropertySpec> propSpecs;
        private readonly List<EventSpec> eventSpec;
        private readonly bool generateClient;
        private readonly bool generateServer;
        private readonly bool defaultImpl;

        public DotNetService(
            string readRequesterName,
            string writeRequesterName,
            string readResponderName,
            string writeResponderName,
            string projectName,
            CodeName genNamespace,
            CodeName serviceName,
            List<ActionSpec> actionSpecs,
            List<PropertySpec> propSpecs,
            List<EventSpec> eventSpec,
            bool generateClient,
            bool generateServer,
            bool defaultImpl)
        {
            this.readRequesterName = new CodeName(readRequesterName);
            this.writeRequesterName = new CodeName(writeRequesterName);
            this.readResponderName = new CodeName(readResponderName);
            this.writeResponderName = new CodeName(writeResponderName);
            this.projectName = projectName;
            this.genNamespace = genNamespace;
            this.serviceName = serviceName;
            this.actionSpecs = actionSpecs;
            this.propSpecs = propSpecs;
            this.eventSpec = eventSpec;
            this.generateClient = generateClient;
            this.generateServer = generateServer;
            this.defaultImpl = defaultImpl;
        }

        public string FileName { get => $"{this.serviceName.GetFileName(TargetLanguage.CSharp)}.g.cs"; }

        public string FolderPath { get => this.genNamespace.GetFolderName(TargetLanguage.CSharp); }
    }
}
