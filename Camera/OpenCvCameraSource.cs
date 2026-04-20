using System;
using System.Diagnostics.CodeAnalysis;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Utilities;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Camera
{
    public class OpenCvCameraSource : ICameraSource
    {
        // DSHOW auto-exposure uses these sentinel values instead of a boolean.
        private const double DshowAutoExposure = 0.75;
        private const double DshowManualExposure = 0.25;

        private readonly CameraConfig _config;
        private VideoCapture? _capture;

        private double _uvcZoomBaseline;
        private double _uvcZoomRange = 250.0; // best-effort guess; Logitech range is opaque

        public OpenCvCameraSource(CameraConfig config)
        {
            _config = config;
            CurrentZoomFactor = config.InitialZoomFactor;
            CurrentExposure = config.InitialExposure;
            AutoExposureOn = config.AutoExposure;
        }

        public bool IsOpen { get; private set; }
        public int FrameWidth { get; private set; }
        public int FrameHeight { get; private set; }
        public double FramesPerSecond { get; private set; }
        public int DeviceIndex { get; private set; } = -1;

        public bool HardwareZoomSupported { get; private set; }
        public double CurrentZoomFactor { get; private set; }
        public double CurrentExposure { get; private set; }
        public bool AutoExposureOn { get; private set; }

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

            ApplyInitialExposure(capture);
            ProbeHardwareZoom(capture);
            ApplyInitialZoom(capture);

            LogNegotiation();
            return true;
        }

        private void LogNegotiation()
        {
            int reqW = _config.FrameWidth;
            int reqH = _config.FrameHeight;
            double reqFps = _config.TargetFps;

            bool resOk = FrameWidth == reqW && FrameHeight == reqH;
            bool fpsOk = reqFps <= 0 || Math.Abs(FramesPerSecond - reqFps) / reqFps <= 0.05;

            string summary = $"Camera negotiated: {FrameWidth}x{FrameHeight} @ {FramesPerSecond:F1} fps (requested {reqW}x{reqH} @ {reqFps:F0} fps, camera index {DeviceIndex}).";

            if (resOk && fpsOk)
            {
                Logger.Info(summary + " OK.");
                return;
            }

            var reasons = new System.Text.StringBuilder();
            if (!resOk) reasons.Append("resolution downgraded");
            if (!resOk && !fpsOk) reasons.Append(", ");
            if (!fpsOk)
            {
                double pct = reqFps > 0 ? (1.0 - FramesPerSecond / reqFps) * 100.0 : 0.0;
                reasons.Append($"fps reduced by {pct:F0}%");
            }

            Logger.Warn(summary + " " + reasons + ".");
        }

        private void ApplyInitialExposure(VideoCapture capture)
        {
            if (_config.AutoExposure)
            {
                capture.Set(VideoCaptureProperties.AutoExposure, DshowAutoExposure);
                AutoExposureOn = true;
                Logger.Info("Exposure: auto.");
            }
            else
            {
                capture.Set(VideoCaptureProperties.AutoExposure, DshowManualExposure);
                capture.Set(VideoCaptureProperties.Exposure, _config.InitialExposure);
                double readback = capture.Get(VideoCaptureProperties.Exposure);
                CurrentExposure = readback;
                AutoExposureOn = false;
                Logger.Info($"Exposure: manual, requested {_config.InitialExposure:F1}, camera reports {readback:F1}.");
            }
        }

        private void ProbeHardwareZoom(VideoCapture capture)
        {
            double baseline = capture.Get(VideoCaptureProperties.Zoom);
            double probeTarget = baseline + 1.0;

            capture.Set(VideoCaptureProperties.Zoom, probeTarget);
            double readback = capture.Get(VideoCaptureProperties.Zoom);
            bool tracked = Math.Abs(readback - probeTarget) < 0.5;

            capture.Set(VideoCaptureProperties.Zoom, baseline);

            HardwareZoomSupported = tracked;
            _uvcZoomBaseline = baseline;

            Logger.Info(HardwareZoomSupported
                ? $"Zoom: hardware UVC zoom supported (baseline={baseline:F0})."
                : "Zoom: hardware UVC zoom not available; digital crop+resize will be used.");
        }

        private void ApplyInitialZoom(VideoCapture capture)
        {
            if (_config.InitialZoomFactor <= 1.0) return;
            double clamped = Math.Clamp(_config.InitialZoomFactor, _config.MinZoomFactor, _config.MaxZoomFactor);
            CurrentZoomFactor = clamped;
            if (HardwareZoomSupported)
            {
                capture.Set(VideoCaptureProperties.Zoom, FactorToUvc(clamped));
            }
        }

        private double FactorToUvc(double factor)
        {
            double normalized = (factor - _config.MinZoomFactor) / Math.Max(0.0001, _config.MaxZoomFactor - _config.MinZoomFactor);
            return _uvcZoomBaseline + normalized * _uvcZoomRange;
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

            if (!HardwareZoomSupported && CurrentZoomFactor > 1.0)
            {
                mat = ApplyDigitalZoom(mat, CurrentZoomFactor);
            }

            frame = new FrameData(mat, DateTime.UtcNow);
            return true;
        }

        private static Mat ApplyDigitalZoom(Mat source, double factor)
        {
            int w = source.Width;
            int h = source.Height;
            int cropW = Math.Max(1, (int)(w / factor));
            int cropH = Math.Max(1, (int)(h / factor));
            int x = (w - cropW) / 2;
            int y = (h - cropH) / 2;

            using (source)
            {
                using var sub = new Mat(source, new Rect(x, y, cropW, cropH));
                var zoomed = new Mat();
                Cv2.Resize(sub, zoomed, new Size(w, h));
                return zoomed;
            }
        }

        public void AdjustZoom(double delta)
        {
            double newFactor = Math.Clamp(CurrentZoomFactor + delta, _config.MinZoomFactor, _config.MaxZoomFactor);
            if (Math.Abs(newFactor - CurrentZoomFactor) < 1e-6) return;

            CurrentZoomFactor = newFactor;

            if (HardwareZoomSupported && _capture is not null)
            {
                _capture.Set(VideoCaptureProperties.Zoom, FactorToUvc(newFactor));
            }

            Logger.Info($"Zoom: {newFactor:F2}x ({(HardwareZoomSupported ? "hardware" : "digital")}).");
        }

        public void AdjustExposure(double delta)
        {
            if (_capture is null) return;

            if (AutoExposureOn)
            {
                _capture.Set(VideoCaptureProperties.AutoExposure, DshowManualExposure);
                AutoExposureOn = false;
                Logger.Info("Exposure: switched auto -> manual.");
            }

            double newExposure = Math.Clamp(CurrentExposure + delta, _config.ExposureMin, _config.ExposureMax);
            if (Math.Abs(newExposure - CurrentExposure) < 1e-6) return;

            _capture.Set(VideoCaptureProperties.Exposure, newExposure);
            double readback = _capture.Get(VideoCaptureProperties.Exposure);
            CurrentExposure = readback;
            Logger.Info($"Exposure: requested {newExposure:F1}, camera reports {readback:F1}.");
        }

        public void ToggleAutoExposure()
        {
            if (_capture is null) return;

            if (AutoExposureOn)
            {
                _capture.Set(VideoCaptureProperties.AutoExposure, DshowManualExposure);
                _capture.Set(VideoCaptureProperties.Exposure, CurrentExposure);
                AutoExposureOn = false;
                Logger.Info($"Exposure: manual at {CurrentExposure:F1}.");
            }
            else
            {
                _capture.Set(VideoCaptureProperties.AutoExposure, DshowAutoExposure);
                AutoExposureOn = true;
                Logger.Info("Exposure: auto.");
            }
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
