using System;
using ComputerVision_LED_Console.Config;

namespace ComputerVision_LED_Console.Camera
{
    public class OpenCvCameraSource : ICameraSource
    {
        private readonly CameraConfig _config;

        public OpenCvCameraSource(CameraConfig config)
        {
            _config = config;
        }

        public bool IsOpen { get; private set; }
        public int FrameWidth { get; private set; }
        public int FrameHeight { get; private set; }
        public double FramesPerSecond { get; private set; }

        public bool Open() => throw new NotImplementedException();
        public bool TryReadFrame(out FrameData frame) => throw new NotImplementedException();
        public void Close() => throw new NotImplementedException();
        public void Dispose() => Close();
    }
}
