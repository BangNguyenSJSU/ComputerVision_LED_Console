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
            ConfigStore.TryLoad(out var snapshot);
            var config = snapshot.App;
            ITimeProvider time = new SystemTimeProvider();
            var status = new StatusService();
            var detector = new LedDetector(config.Detection, time);
            if (snapshot.Markers.Count > 0) detector.RestoreMarkers(snapshot.Markers);
            var state = new AppState();
            var renderer = new OverlayRenderer(state);
            using var camera = new OpenCvCameraSource(config.Camera);
            var input = new InputHandler(detector, state, config.Detection, camera, config.Camera);

            var publishers = new List<IStatusPublisher>();
            if (config.Network.HttpEnabled)
            {
                publishers.Add(new HttpStatusServer(config.Network, status));
            }
            if (config.Network.TcpEnabled)
            {
                publishers.Add(new TcpStatusServer(config.Network, status));
            }
            if (config.Network.TcpBinaryEnabled)
            {
                publishers.Add(new TcpBinaryStatusServer(config.Network, status));
            }

            var app = new AppController(config, camera, detector, status, time, renderer, input, state, publishers);
            app.Run();
        }
    }
}
