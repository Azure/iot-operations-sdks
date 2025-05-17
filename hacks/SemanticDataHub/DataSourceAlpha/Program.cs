namespace DataSourceAlpha
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading.Tasks;

    internal class Program
    {
        const int periodSeconds = 5;
        const double baseTemp = 44.0;
        const double tempSlope = 1.5;
        const string pipeName = "DataSourceAlpha";

        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: DataSourceAlpha <SECONDS_TO_RUN>");
                return;
            }

            int runSeconds = int.Parse(args[0], CultureInfo.InvariantCulture);

            using (NamedPipeServerStream pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.Out))
            {
                Console.Write($"Waiting for client connection to named pipe '{pipeName}' ...");
                pipeServer.WaitForConnection();
                Console.WriteLine(" Client connected.");

                try
                {
                    using (StreamWriter pipeWriter = new StreamWriter(pipeServer))
                    {
                        pipeWriter.AutoFlush = true;

                        double risingSawtooth = baseTemp;
                        double fallingSawtooth = baseTemp;
                        double triangle = baseTemp;
                        bool upward = true;

                        for (int i = 0; i < runSeconds; i++)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(1));

                            risingSawtooth += tempSlope;
                            fallingSawtooth -= tempSlope;
                            triangle += (upward ? 1.0 : -1.0) * tempSlope;

                            string risingMode = "Rising";
                            string fallingMode = "Falling";
                            string triangleMode = upward ? "Rising" : "Falling";

                            if (i % periodSeconds == 0)
                            {
                                risingSawtooth = baseTemp;
                                fallingSawtooth = baseTemp;
                                upward = !upward;

                                risingMode = "Reset";
                                fallingMode = "Reset";
                            }

                            pipeWriter.WriteLine($"risingSawtooth,{risingSawtooth},{risingMode}");
                            pipeWriter.WriteLine($"fallingSawtooth,{fallingSawtooth},{fallingMode}");
                            pipeWriter.WriteLine($"triangle,{triangle},{triangleMode}");
                        }
                    }
                }
                catch (IOException e)
                {
                    Console.WriteLine("ERROR: {0}", e.Message);
                }
            }
        }
    }
}
