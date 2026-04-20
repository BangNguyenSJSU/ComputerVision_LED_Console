using System;
using System.Diagnostics.CodeAnalysis;

namespace ComputerVision_LED_Console.Camera
{
    public interface ICameraSource : IDisposable
    {
        bool IsOpen { get; }
        int DeviceIndex { get; }
        int FrameWidth { get; }
        int FrameHeight { get; }
        double FramesPerSecond { get; }

        bool HardwareZoomSupported { get; }
        double CurrentZoomFactor { get; }
        double CurrentExposure { get; }
        bool AutoExposureOn { get; }

        bool Open();
        bool TryReadFrame([NotNullWhen(true)] out FrameData? frame);
        void Close();

        void AdjustZoom(double delta);
        void AdjustExposure(double delta);
        void ToggleAutoExposure();
    }
}
