using System.Collections.Generic;
using ComputerVision_LED_Console.App;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Network;
using ComputerVision_LED_Console.Services;
using ComputerVision_LED_Console.Utilities;
using ComputerVision_LED_Console.Vision;

namespace ComputerVision_LED_Console
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var config = new AppConfig();
            ITimeProvider time = new SystemTimeProvider();
            var status = new StatusService();
            var detector = new LedDetector(config.Detection, time);
            var state = new AppState();
            var renderer = new OverlayRenderer(state);
            var input = new InputHandler(detector, state, config.Detection);
            using var camera = new OpenCvCameraSource(config.Camera);

            var publishers = new List<IStatusPublisher>();
            if (config.Network.HttpEnabled)
            {
                publishers.Add(new HttpStatusServer(config.Network, status));
            }
            if (config.Network.TcpEnabled)
            {
                publishers.Add(new TcpStatusServer(config.Network, status));
            }

            var app = new AppController(config, camera, detector, status, time, renderer, input, state, publishers);
            app.Run();
        }
    }
}
