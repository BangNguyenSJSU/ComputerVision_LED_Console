using System;

namespace ComputerVision_LED_Console.Camera
{
    public interface ICameraSource : IDisposable
    {
        bool IsOpen { get; }
        int FrameWidth { get; }
        int FrameHeight { get; }
        double FramesPerSecond { get; }

        bool Open();
        bool TryReadFrame(out FrameData frame);
        void Close();
    }
}
