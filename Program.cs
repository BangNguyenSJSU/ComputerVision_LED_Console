using System;
using System.Linq;
using ComputerVision_LED_Console.App;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Services;
using ComputerVision_LED_Console.Utilities;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console
{
    internal class Program
    {
        const string WindowName = "LED Detection";

        static void Main(string[] args)
        {
            var config = new AppConfig();
            ITimeProvider time = new SystemTimeProvider();
            var status = new StatusService();
            var detector = new LedDetector(config.Detection, time);
            var state = new AppState();

            using var camera = new OpenCvCameraSource(config.Camera);
            if (!camera.Open())
            {
                return;
            }

            state.DisplayScale = camera.FrameWidth > 1280 ? 1280.0 / camera.FrameWidth : 1.0;
            var renderer = new OverlayRenderer(state.DisplayScale);
            var input = new InputHandler(detector, state, config.Detection);

            Console.WriteLine("Controls: Q/ESC=quit | R=rescan | Left-click=add/drag | Right-click=delete");
            Console.WriteLine($"Per-LED thresholds (defaults On={config.Detection.DefaultOnThreshold}, Off={config.Detection.DefaultOffThreshold}). Left-click an LED to select it.");
            Console.WriteLine("  - C = auto-calibrate selected LED: press while lit, then press again while dark.");
            Console.WriteLine("  - [ / ]  nudge On threshold by 5 (down / up).");
            Console.WriteLine("  - ; / '  nudge Off threshold by 5 (down / up).");
            Console.WriteLine($"  - Minimum gap of {config.Detection.MinThresholdGap} is enforced to prevent flicker.");

            bool initialized = false;

            Cv2.NamedWindow(WindowName);
            Cv2.SetMouseCallback(WindowName, input.OnMouse);

            while (true)
            {
                if (!camera.TryReadFrame(out var frameData))
                {
                    continue;
                }

                using var fd = frameData;
                Mat frame = fd.Frame;

                if (!initialized)
                {
                    detector.AutoDetect(fd);
                    initialized = true;
                }

                var results = detector.EvaluateAll(fd);

                status.Update(new SystemStatus
                {
                    TimestampUtc = time.UtcNow,
                    CameraIndex = camera.DeviceIndex,
                    FrameWidth = camera.FrameWidth,
                    FrameHeight = camera.FrameHeight,
                    FramesPerSecond = camera.FramesPerSecond,
                    Leds = results.ToList(),
                });

                renderer.RenderAndShow(WindowName, frame, detector.Rois, results, state.SelectedMarkerIndex);

                var action = input.HandleKey(Cv2.WaitKey(1));
                if (action == KeyAction.Quit) break;
                if (action == KeyAction.Rescan) detector.AutoDetect(fd);
            }

            Cv2.DestroyAllWindows();
        }
    }
}
