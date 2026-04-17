using System;

namespace ComputerVision_LED_Console.Network
{
    public interface IStatusPublisher : IDisposable
    {
        bool IsRunning { get; }
        void Start();
        void Stop();
    }
}
