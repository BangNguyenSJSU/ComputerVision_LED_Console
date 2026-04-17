using System;
using System.Diagnostics.CodeAnalysis;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Utilities;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Camera
{
    public class OpenCvCameraSource : ICameraSource
    {
        private readonly CameraConfig _config;
        private VideoCapture? _capture;

        public OpenCvCameraSource(CameraConfig config)
        {
            _config = config;
        }

        public bool IsOpen { get; private set; }
        public int FrameWidth { get; private set; }
        public int FrameHeight { get; private set; }
        public double FramesPerSecond { get; private set; }
        public int DeviceIndex { get; private set; } = -1;

        public bool Open()
        {
            Logger.Info("Scanning for available cameras...");
            var available = CameraProbe.ScanAvailable(_config.MaxProbeIndex);
            int chosen = CameraProbe.PromptUserSelection(available);
            if (chosen < 0)
            {
                return false;
            }

            var capture = new VideoCapture(chosen, VideoCaptureAPIs.DSHOW);
            if (!capture.IsOpened())
            {
                Logger.Error($"Failed to open camera at index {chosen}.");
                capture.Dispose();
                return false;
            }

            string fc = _config.FourCC;
            capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC(fc[0], fc[1], fc[2], fc[3]));
            capture.Set(VideoCaptureProperties.FrameWidth, _config.FrameWidth);
            capture.Set(VideoCaptureProperties.FrameHeight, _config.FrameHeight);
            capture.Set(VideoCaptureProperties.Fps, _config.TargetFps);
            capture.Set(VideoCaptureProperties.BufferSize, _config.BufferSize);

            FrameWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
            FrameHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);
            FramesPerSecond = capture.Get(VideoCaptureProperties.Fps);
            DeviceIndex = chosen;

            _capture = capture;
            IsOpen = true;

            Logger.Info($"Using camera index {DeviceIndex} at {FrameWidth}x{FrameHeight} @ {FramesPerSecond:F1} fps.");
            return true;
        }

        public bool TryReadFrame([NotNullWhen(true)] out FrameData? frame)
        {
            if (_capture is null || !IsOpen)
            {
                frame = null;
                return false;
            }

            var mat = new Mat();
            _capture.Read(mat);
            if (mat.Empty())
            {
                mat.Dispose();
                frame = null;
                return false;
            }

            frame = new FrameData(mat, DateTime.UtcNow);
            return true;
        }

        public void Close()
        {
            if (_capture is not null)
            {
                _capture.Release();
                _capture.Dispose();
                _capture = null;
            }
            IsOpen = false;
        }

        public void Dispose() => Close();
    }
}
