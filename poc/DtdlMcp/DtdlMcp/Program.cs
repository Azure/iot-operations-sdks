namespace DtdlMcp
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using ModelContextProtocol.Protocol;

    internal class Program
    {
        static async Task Main(string[] args)
        {
/*
            DtdlModelTextService service = new DtdlModelTextService();
            var result = service.GetGeneratedClassFileNames("dtmi:opcua:MTConnect_v2:MTMessageEventType;1", "McpDemo");
            if (result.IsError ?? false)
            {
                Console.WriteLine("Error generating code: " + string.Join(Environment.NewLine, result.Content.Select(cb => ((TextContentBlock)cb).Text)));
                return;
            }
            else
            {
                Console.WriteLine("Class file names:");
                foreach (var contentBlock in result.Content)
                {
                    if (contentBlock is TextContentBlock textContent)
                    {
                        Console.WriteLine(textContent.Text);
                    }
                }
                return;
            }
*/
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                // Configure all logs to go to stderr
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
            });

            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();

            builder.Services.AddSingleton<DtdlModelTextService>();

            await builder.Build().RunAsync();
        }
    }
}
