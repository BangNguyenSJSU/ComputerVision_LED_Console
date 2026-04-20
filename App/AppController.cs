using System;
using System.Collections.Generic;
using System.Linq;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Network;
using ComputerVision_LED_Console.Services;
using ComputerVision_LED_Console.Utilities;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console.App
{
    public class AppController
    {
        private const string WindowName = "LED Detection";

        private readonly AppConfig _config;
        private readonly ICameraSource _camera;
        private readonly LedDetector _detector;
        private readonly StatusService _status;
        private readonly ITimeProvider _time;
        private readonly OverlayRenderer _renderer;
        private readonly InputHandler _input;
        private readonly AppState _state;
        private readonly IReadOnlyList<IStatusPublisher> _publishers;
        private MouseCallback? _mouseCallback;

        public AppController(
            AppConfig config,
            ICameraSource camera,
            LedDetector detector,
            StatusService status,
            ITimeProvider time,
            OverlayRenderer renderer,
            InputHandler input,
            AppState state,
            IReadOnlyList<IStatusPublisher>? publishers = null)
        {
            _config = config;
            _camera = camera;
            _detector = detector;
            _status = status;
            _time = time;
            _renderer = renderer;
            _input = input;
            _state = state;
            _publishers = publishers ?? Array.Empty<IStatusPublisher>();
        }

        public void Run()
        {
            if (!_camera.Open()) return;

            int displayMax = _config.Camera.DisplayMaxWidth;
            _state.DisplayScale = _camera.FrameWidth > displayMax
                ? (double)displayMax / _camera.FrameWidth
                : 1.0;

            PrintControlsHelp();

            Cv2.NamedWindow(WindowName);
            _mouseCallback = _input.OnMouse;
            Cv2.SetMouseCallback(WindowName, _mouseCallback);

            foreach (var pub in _publishers) pub.Start();

            try
            {
                Loop();
            }
            finally
            {
                foreach (var pub in _publishers) pub.Stop();
                Cv2.DestroyAllWindows();
            }
        }

        private void Loop()
        {
            bool initialized = false;

            while (true)
            {
                if (!_camera.TryReadFrame(out var frameData))
                {
                    continue;
                }

                using var fd = frameData;
                Mat frame = fd.Frame;

                if (!initialized)
                {
                    if (_detector.Rois.Count == 0)
                    {
                        _detector.AutoDetect(fd);
                    }
                    else
                    {
                        Logger.Info($"Skipping auto-detect; {_detector.Rois.Count} marker(s) restored from config.");
                    }
                    initialized = true;
                }

                var results = _detector.EvaluateAll(fd);

                _status.Update(new SystemStatus
                {
                    TimestampUtc = _time.UtcNow,
                    CameraIndex = _camera.DeviceIndex,
                    FrameWidth = _camera.FrameWidth,
                    FrameHeight = _camera.FrameHeight,
                    FramesPerSecond = _camera.FramesPerSecond,
                    Leds = results.ToList(),
                });

                _renderer.RenderAndShow(WindowName, frame, _detector.Rois, results, _state.SelectedMarkerIndex);

                var action = _input.HandleKey(Cv2.WaitKey(1));
                if (action == KeyAction.Quit)
                {
                    ConfigStore.Save(_config, _detector.Rois);
                    break;
                }
                if (action == KeyAction.Rescan) _detector.AutoDetect(fd);
                if (action == KeyAction.Save) ConfigStore.Save(_config, _detector.Rois);
            }
        }

        private void PrintControlsHelp()
        {
            Console.WriteLine("Controls: Q/ESC=quit | R=rescan | Left-click=add/drag | Right-click=delete");
            Console.WriteLine($"Per-LED thresholds (defaults On={_config.Detection.DefaultOnThreshold}, Off={_config.Detection.DefaultOffThreshold}). Left-click an LED to select it.");
            Console.WriteLine("  - C = auto-calibrate selected LED: press while lit, then press again while dark.");
            Console.WriteLine($"  - [ / ]  nudge On threshold by {_config.Detection.ThresholdStep} (down / up).");
            Console.WriteLine($"  - ; / '  nudge Off threshold by {_config.Detection.ThresholdStep} (down / up).");
            Console.WriteLine($"  - Minimum gap of {_config.Detection.MinThresholdGap} is enforced to prevent flicker.");
            Console.WriteLine($"  - + / -  zoom in / out (step {_config.Camera.ZoomStep:F2}x, range {_config.Camera.MinZoomFactor:F1}..{_config.Camera.MaxZoomFactor:F1}x).");
            Console.WriteLine($"  - . / ,  exposure up / down (step {_config.Camera.ExposureStep:F1}, auto-switches to manual).");
            Console.WriteLine("  - A      toggle auto-exposure on / off.");
            Console.WriteLine($"  - S      save markers + settings to {ConfigStore.FileName} (also auto-saves on quit).");
        }
    }
}
