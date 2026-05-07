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
        private readonly AuthGate _auth;
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
            AuthGate auth,
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
            _auth = auth;
            _publishers = publishers ?? Array.Empty<IStatusPublisher>();
        }

        public void Run()
        {
            RunFirstRunPasswordSetupIfNeeded();

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

                _input.UpdateColorSampler((x, y) => _detector.ClassifyColorAt(fd, x, y));
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
                    if (_auth.IsUnlocked) ConfigStore.Save(_config, _detector.Rois);
                    else Logger.Info("Locked — skipping auto-save on quit.");
                    break;
                }
                if (action == KeyAction.Rescan) _detector.AutoDetect(fd);
                if (action == KeyAction.Save)
                {
                    if (_auth.IsUnlocked) ConfigStore.Save(_config, _detector.Rois);
                    else Logger.Warn("Locked — save ignored. Press U to unlock.");
                }
                if (action == KeyAction.UnlockPrompt) PromptForUnlock();
                if (action == KeyAction.Lock)
                {
                    _auth.Lock();
                    Logger.Info("Locked.");
                }
                if (action == KeyAction.PasswordToggle) PromptForPasswordToggle();
            }
        }

        private void RunFirstRunPasswordSetupIfNeeded()
        {
            if (!_config.Security.LockEnabled) return;
            if (_auth.IsConfigured) return;

            Console.WriteLine();
            Console.WriteLine("Settings lock is enabled but no password is set.");
            Console.Write("Enter a password to enable the lock (or press Enter to leave it disabled for this session): ");
            string? input = ConsoleHelpers.ReadMaskedLine();
            if (string.IsNullOrEmpty(input))
            {
                Logger.Warn("Settings lock left disabled — no password set.");
                return;
            }

            var (hash, salt) = _auth.HashNewPassword(input);
            _config.Security.PasswordHash = hash;
            _config.Security.PasswordSalt = salt;
            ConfigStore.Save(_config, _detector.Rois);
            _auth.Lock();
            Logger.Info("Password set — app starts locked. Press U to unlock.");
        }

        private void PromptForUnlock()
        {
            if (!_auth.IsConfigured)
            {
                Logger.Warn("No password configured — nothing to unlock.");
                return;
            }
            Console.Write("Password: ");
            string? input = ConsoleHelpers.ReadMaskedLine();
            if (input is null) return;
            if (_auth.TryUnlock(input)) Logger.Info("Unlocked.");
            else Logger.Warn("Wrong password.");
        }

        private void PromptForPasswordToggle()
        {
            if (!_auth.IsConfigured)
            {
                Logger.Warn("No password configured — set one with the first-run prompt or by editing the config file.");
                return;
            }
            Console.Write(_auth.IsUnlocked ? "Password to lock: " : "Password to unlock: ");
            string? input = ConsoleHelpers.ReadMaskedLine();
            if (input is null) return;

            if (_auth.IsUnlocked)
            {
                if (_auth.VerifyPassword(input))
                {
                    _auth.Lock();
                    Logger.Info("Locked.");
                }
                else
                {
                    Logger.Warn("Wrong password.");
                }
            }
            else
            {
                if (_auth.TryUnlock(input)) Logger.Info("Unlocked.");
                else Logger.Warn("Wrong password.");
            }
        }

        private void PrintControlsHelp()
        {
            Console.WriteLine("Controls: Q/ESC=quit | R=rescan | Left-click=add/drag | Right-click=delete | Mouse-wheel=resize ROI");
            Console.WriteLine($"Per-LED thresholds (defaults On={_config.Detection.DefaultOnThreshold}, Off={_config.Detection.DefaultOffThreshold}). Left-click an LED to select it.");
            Console.WriteLine("  - C = auto-calibrate selected LED: press while lit, then press again while dark.");
            Console.WriteLine($"  - [ / ]  nudge On threshold by {_config.Detection.ThresholdStep} (down / up).");
            Console.WriteLine($"  - ; / '  nudge Off threshold by {_config.Detection.ThresholdStep} (down / up).");
            Console.WriteLine($"  - Minimum gap of {_config.Detection.MinThresholdGap} is enforced to prevent flicker.");
            Console.WriteLine($"  - + / -  zoom in / out (step {_config.Camera.ZoomStep:F2}x, range {_config.Camera.MinZoomFactor:F1}..{_config.Camera.MaxZoomFactor:F1}x).");
            Console.WriteLine($"  - . / ,  exposure up / down (step {_config.Camera.ExposureStep:F1}, auto-switches to manual).");
            Console.WriteLine("  - A      toggle auto-exposure on / off.");
            Console.WriteLine($"  - S      save markers + settings to {ConfigStore.FileName} (also auto-saves on quit).");
            Console.WriteLine("  - U / L  unlock prompt / lock immediately.");
            Console.WriteLine("  - P      password prompt: enter password to toggle lock state.");
        }
    }
}
