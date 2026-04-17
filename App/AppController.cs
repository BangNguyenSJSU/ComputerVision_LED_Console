using System;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Network;
using ComputerVision_LED_Console.Services;
using ComputerVision_LED_Console.Vision;

namespace ComputerVision_LED_Console.App
{
    public class AppController
    {
        private readonly AppConfig _config;
        private readonly ICameraSource _camera;
        private readonly ILedDetector _detector;
        private readonly StatusService _status;
        private readonly IStatusPublisher _httpPublisher;
        private readonly IStatusPublisher _tcpPublisher;
        private readonly AppState _state = new AppState();

        public AppController(
            AppConfig config,
            ICameraSource camera,
            ILedDetector detector,
            StatusService status,
            IStatusPublisher httpPublisher,
            IStatusPublisher tcpPublisher)
        {
            _config = config;
            _camera = camera;
            _detector = detector;
            _status = status;
            _httpPublisher = httpPublisher;
            _tcpPublisher = tcpPublisher;
        }

        public void Run() => throw new NotImplementedException();
        public void Stop() => _state.IsRunning = false;
    }
}
