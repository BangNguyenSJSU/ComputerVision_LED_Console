using System;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Services;

namespace ComputerVision_LED_Console.Network
{
    public class TcpStatusServer : IStatusPublisher
    {
        private readonly NetworkConfig _config;
        private readonly StatusService _status;

        public TcpStatusServer(NetworkConfig config, StatusService status)
        {
            _config = config;
            _status = status;
        }

        public bool IsRunning { get; private set; }

        public void Start() => throw new NotImplementedException();
        public void Stop() => throw new NotImplementedException();
        public void Dispose() => Stop();
    }
}
