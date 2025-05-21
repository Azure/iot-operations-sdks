namespace DataSourceAlpha
{
    using System;
    using System.IO;
    using System.IO.Pipes;
    using System.Threading.Tasks;
    using Common;

    internal class Program
    {
        const double extBaseTemp = 152.0;
        const double extTempVar = 3.0;
        const double intBaseTemp = 132.0;
        const double intTempVar = 3.0;
        const double envBaseTemp = 92.0;
        const double envTempVar = 9.0;

        const double basePressure = 8.0;
        const double pressureVar = 4.0;

        const double posMagnitude = 6.0;
        const double posAngleInc = 0.5;

        static readonly TimeSpan tempDelay = TimeSpan.FromSeconds(1);
        static readonly TimeSpan tempInterval = TimeSpan.FromSeconds(6);

        static readonly TimeSpan presDelay = TimeSpan.FromSeconds(2);
        static readonly TimeSpan presInterval = TimeSpan.FromSeconds(6);

        static readonly TimeSpan statDelay = TimeSpan.FromSeconds(4);
        static readonly TimeSpan statInterval = TimeSpan.FromSeconds(12);

        static readonly TimeSpan posDelay = TimeSpan.FromSeconds(0);
        static readonly TimeSpan posInterval = TimeSpan.FromSeconds(3);

        static readonly TimeSpan modeDelay = TimeSpan.FromSeconds(10);
        static readonly TimeSpan modeInterval = TimeSpan.FromSeconds(12);

        static async Task SendTemperature(StreamWriter pipeWriter)
        {
            await Task.Delay(tempDelay);

            Random rand = new ();
            while (true)
            {
                try
                {
                    double extTemp = extBaseTemp + rand.NextDouble() * extTempVar;
                    double intTemp = intBaseTemp + rand.NextDouble() * intTempVar;
                    double envTemp = envBaseTemp + rand.NextDouble() * envTempVar;

                    string data = $"temps,{extTemp:F2},{intTemp:F2},{envTemp:F2}";

                    Console.WriteLine(data);
                    pipeWriter.WriteLine(data);
                }
                catch (IOException)
                {
                    return;
                }

                await Task.Delay(tempInterval);
            }
        }

        static async Task SendPressure(StreamWriter pipeWriter)
        {
            await Task.Delay(presDelay);

            Random rand = new();
            while (true)
            {
                try
                {
                    double pressure = basePressure + rand.NextDouble() * pressureVar;

                    string data = $"pres,{pressure:F2},-1,-1";

                    Console.WriteLine(data);
                    pipeWriter.WriteLine(data);
                }
                catch (IOException)
                {
                    return;
                }

                await Task.Delay(presInterval);
            }
        }

        static async Task SendStatus(StreamWriter pipeWriter)
        {
            await Task.Delay(statDelay);

            while (true)
            {
                try
                {

                    string data = "stat,1,0.01";

                    Console.WriteLine(data);
                    pipeWriter.WriteLine(data);
                }
                catch (IOException)
                {
                    return;
                }

                await Task.Delay(statInterval);
            }
        }

        static async Task SendPosition(StreamWriter pipeWriter)
        {
            await Task.Delay(posDelay);

            double angle = 0.0;

            while (true)
            {
                angle += posAngleInc;

                try
                {
                    double x = posMagnitude / Math.Cos(angle);
                    double y = posMagnitude / Math.Sin(angle);

                    string data = $"pos,{x:F3},{y:F3}";

                    Console.WriteLine(data);
                    pipeWriter.WriteLine(data);
                }
                catch (IOException)
                {
                    return;
                }

                await Task.Delay(posInterval);
            }
        }

        static async Task SendMode(StreamWriter pipeWriter)
        {
            await Task.Delay(modeDelay);

            Random rand = new();
            while (true)
            {
                try
                {
                    string mode = rand.NextDouble() < 0.1 ? "calibrate" : "scan";

                    string data = $"mode,{mode}";

                    Console.WriteLine(data);
                    pipeWriter.WriteLine(data);
                }
                catch (IOException)
                {
                    return;
                }

                await Task.Delay(modeInterval);
            }
        }

        static async Task Main(string[] args)
        {
            while (true)
            {
                using (NamedPipeServerStream pipeServer = new NamedPipeServerStream(Constants.AlphaDeviceAddress, PipeDirection.Out))
                {
                    Console.Write($"Waiting for client connection to named pipe '{Constants.AlphaDeviceAddress}' ...");
                    pipeServer.WaitForConnection();
                    Console.WriteLine(" Client connected.");

                    try
                    {
                        using (StreamWriter pipeWriter = new StreamWriter(pipeServer))
                        {
                            pipeWriter.AutoFlush = true;

                            Task sendTempTask = Task.Run(async () => await SendTemperature(pipeWriter));
                            Task sendPresTask = Task.Run(async () => await SendPressure(pipeWriter));
                            Task sendStatTask = Task.Run(async () => await SendStatus(pipeWriter));
                            Task sendPosTask = Task.Run(async () => await SendPosition(pipeWriter));
                            Task sendModeTask = Task.Run(async () => await SendMode(pipeWriter));

                            await Task.WhenAll(new Task[] { sendTempTask, sendPresTask, sendStatTask, sendPosTask, sendModeTask });
                        }
                    }
                    catch (IOException)
                    {
                        Console.WriteLine("Client disconnected.");
                    }
                }
            }
        }
    }
}
