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

            _state.DisplayScale = _camera.FrameWidth > 1280 ? 1280.0 / _camera.FrameWidth : 1.0;

            PrintControlsHelp();

            Cv2.NamedWindow(WindowName);
            Cv2.SetMouseCallback(WindowName, _input.OnMouse);

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
                    _detector.AutoDetect(fd);
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
                if (action == KeyAction.Quit) break;
                if (action == KeyAction.Rescan) _detector.AutoDetect(fd);
            }
        }

        private void PrintControlsHelp()
        {
            Console.WriteLine("Controls: Q/ESC=quit | R=rescan | Left-click=add/drag | Right-click=delete");
            Console.WriteLine($"Per-LED thresholds (defaults On={_config.Detection.DefaultOnThreshold}, Off={_config.Detection.DefaultOffThreshold}). Left-click an LED to select it.");
            Console.WriteLine("  - C = auto-calibrate selected LED: press while lit, then press again while dark.");
            Console.WriteLine("  - [ / ]  nudge On threshold by 5 (down / up).");
            Console.WriteLine("  - ; / '  nudge Off threshold by 5 (down / up).");
            Console.WriteLine($"  - Minimum gap of {_config.Detection.MinThresholdGap} is enforced to prevent flicker.");
        }
    }
}
