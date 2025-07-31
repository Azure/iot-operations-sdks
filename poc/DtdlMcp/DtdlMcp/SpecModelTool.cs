namespace DtdlMcp
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    [McpServerToolType, Description("Tools for working with a catalog of DTDL models converted from OPC UA Companion Specs")]
    public static class SpecModelTool
    {
        [McpServerTool, Description("List the names of all spec files in the catalog.")]
        public static string ListSpecFiles(DtdlModelTextService dtdlModelTextService)
        {
            List<SpecInfo> models = dtdlModelTextService.GetModelIndex();

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[");
            bool needComma = false;
            foreach (SpecInfo specInfo in models)
            {
                int eventCount = specInfo.Events.Count;
                int compositeCount = specInfo.Composites.Count;
                int otherTypeCount = specInfo.OtherTypes.Count;
                int totalCount = eventCount + compositeCount + otherTypeCount;

                stringBuilder.AppendLine(needComma ? "," : "");
                stringBuilder.Append($"  {{ \"file name\": \"{specInfo.FileName}\", \"models\": {totalCount}, \"events\": {eventCount}, \"composites\": {compositeCount}, \"other\": {otherTypeCount} }}");
                needComma = true;
            }
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("]");

            return stringBuilder.ToString();
        }

        [McpServerTool, Description("List all models in a given spec file in the catalog.")]
        public static string ListModelsInSpecFile(DtdlModelTextService dtdlModelTextService, [Description("The name of the spec file")] string fileName)
        {
            List<SpecInfo> models = dtdlModelTextService.GetModelIndex();

            SpecInfo? specInfo = models.FirstOrDefault(m => m.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (specInfo == null)
            {
                return "[]";
            }

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("[");
            bool needComma = false;
            foreach (ModelInfo model in specInfo.Events)
            {
                stringBuilder.AppendLine(needComma ? ",": "");
                stringBuilder.Append($"  {{ \"file\": \"{specInfo.FileName}\", \"modelId\": \"{model.ModelId}\", \"displayName\": \"{model.DisplayName}\", \"type\": \"event\" }}");
                needComma = true;
            }

            foreach (ModelInfo model in specInfo.Composites)
            {
                stringBuilder.AppendLine(needComma ? ",": "");
                stringBuilder.Append($"  {{ \"file\": \"{specInfo.FileName}\", \"modelId\": \"{model.ModelId}\", \"displayName\": \"{model.DisplayName}\", \"type\": \"composite\" }}");
                needComma = true;
            }

            foreach (ModelInfo model in specInfo.OtherTypes)
            {
                stringBuilder.AppendLine(needComma ? "," : "");
                stringBuilder.Append($"  {{ \"file\": \"{specInfo.FileName}\", \"modelId\": \"{model.ModelId}\", \"displayName\": \"{model.DisplayName}\", \"type\": \"other\" }}");
                needComma = true;
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("]");

            return stringBuilder.ToString();
        }

        [McpServerTool, Description("Return the names of C# class files (or csproj project files) that will be generated from a specified model. Each returned file name can be submitted to `GenerateClass` to generate C# code for the class file.")]
        public static CallToolResult GetGeneratedClassFileNames(
            DtdlModelTextService dtdlModelTextService,
            [Description("The model ID from which to generate C# classes")] string modelId,
            [Description("The C# namespace to use for the generated class code")] string projectName,
            [Description("True if client-specific class files should be included in the list of generated files")] bool generateClient = false,
            [Description("True if server-specific class files should be included in the list of generated files")] bool generateServer = false,
            [Description("True if the generated code should include default implementations of virtual methods instead of pure abstract methods")] bool defaultImpl = false,
            [Description("True if a csproj project file should be included in the list of generated files")] bool generateProject = false)
        {
            return dtdlModelTextService.GetGeneratedClassFileNames(modelId, projectName, generateClient, generateServer, defaultImpl, generateProject);
        }

        [McpServerTool, Description("Generate C# class from a specified model and a specified class (or csproj) file name.")]
        public static CallToolResult GenerateClass(
            DtdlModelTextService dtdlModelTextService,
            [Description("The model ID from which to generate C# classes")] string modelId,
            [Description("The name of the class (or csproj) file to generate; this argument should be one of the names returned by `GetGeneratedClassFileNames`")] string classFileName)
        {
            return dtdlModelTextService.GetGeneratedClass(modelId, classFileName);
        }
    }
}
