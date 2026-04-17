using System;
using System.Collections.Generic;
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

        static AppConfig _config = null!;
        static ITimeProvider _time = null!;
        static LedDetector _detector = null!;
        static StatusService _status = null!;
        static int DragIndex = -1;
        static int SelectedIndex = -1;
        static double DisplayScale = 1.0;

        static void Main(string[] args)
        {
            _config = new AppConfig();
            _time = new SystemTimeProvider();
            _status = new StatusService();
            _detector = new LedDetector(_config.Detection, _time);

            using var camera = new OpenCvCameraSource(_config.Camera);
            if (!camera.Open())
            {
                return;
            }

            DisplayScale = camera.FrameWidth > 1280 ? 1280.0 / camera.FrameWidth : 1.0;
            var renderer = new OverlayRenderer(DisplayScale);

            Console.WriteLine("Controls: Q/ESC=quit | R=rescan | Left-click=add/drag | Right-click=delete");
            Console.WriteLine($"Per-LED thresholds (defaults On={_config.Detection.DefaultOnThreshold}, Off={_config.Detection.DefaultOffThreshold}). Left-click an LED to select it.");
            Console.WriteLine("  - C = auto-calibrate selected LED: press while lit, then press again while dark.");
            Console.WriteLine("  - [ / ]  nudge On threshold by 5 (down / up).");
            Console.WriteLine("  - ; / '  nudge Off threshold by 5 (down / up).");
            Console.WriteLine($"  - Minimum gap of {_config.Detection.MinThresholdGap} is enforced to prevent flicker.");

            bool initialized = false;

            Cv2.NamedWindow(WindowName);
            Cv2.SetMouseCallback(WindowName, OnMouse);

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
                    _detector.AutoDetect(fd);
                    initialized = true;
                }

                var results = _detector.EvaluateAll(fd);

                _status.Update(new SystemStatus
                {
                    TimestampUtc = _time.UtcNow,
                    CameraIndex = camera.DeviceIndex,
                    FrameWidth = camera.FrameWidth,
                    FrameHeight = camera.FrameHeight,
                    FramesPerSecond = camera.FramesPerSecond,
                    Leds = results.ToList(),
                });

                renderer.RenderAndShow(WindowName, frame, _detector.Rois, results, SelectedIndex);

                int key = Cv2.WaitKey(1);
                if (key == 'q' || key == 'Q' || key == 27)
                {
                    break;
                }
                if (key == 'r' || key == 'R')
                {
                    _detector.Clear();
                    DragIndex = -1;
                    SelectedIndex = -1;
                    _detector.AutoDetect(fd);
                }

                if (SelectedIndex >= 0 && SelectedIndex < _detector.Rois.Count)
                {
                    double step = _config.Detection.ThresholdStep;
                    if (key == 'c' || key == 'C') _detector.ToggleCalibrate(SelectedIndex);
                    else if (key == ']') _detector.TuneOnThreshold(SelectedIndex, +step);
                    else if (key == '[') _detector.TuneOnThreshold(SelectedIndex, -step);
                    else if (key == '\'') _detector.TuneOffThreshold(SelectedIndex, +step);
                    else if (key == ';') _detector.TuneOffThreshold(SelectedIndex, -step);
                }
            }

            Cv2.DestroyAllWindows();
        }

        static void OnMouse(MouseEventTypes @event, int x, int y, MouseEventFlags flags, IntPtr userData)
        {
            float nx = (float)(x / DisplayScale);
            float ny = (float)(y / DisplayScale);

            switch (@event)
            {
                case MouseEventTypes.LButtonDown:
                {
                    int hit = _detector.FindRoiAt(nx, ny);
                    if (hit >= 0)
                    {
                        DragIndex = hit;
                        SelectedIndex = hit;
                    }
                    else
                    {
                        int newIdx = _detector.AddManualRoi(nx, ny);
                        DragIndex = newIdx;
                        SelectedIndex = newIdx;
                    }
                    break;
                }
                case MouseEventTypes.MouseMove:
                    if ((flags & MouseEventFlags.LButton) != 0 && DragIndex >= 0 && DragIndex < _detector.Rois.Count)
                    {
                        _detector.MoveRoi(DragIndex, nx, ny);
                    }
                    break;
                case MouseEventTypes.LButtonUp:
                    DragIndex = -1;
                    break;
                case MouseEventTypes.RButtonDown:
                {
                    int hit = _detector.FindRoiAt(nx, ny);
                    if (hit >= 0)
                    {
                        _detector.RemoveRoiAt(hit);
                        if (DragIndex == hit) DragIndex = -1;
                        else if (DragIndex > hit) DragIndex--;
                        if (SelectedIndex == hit) SelectedIndex = -1;
                        else if (SelectedIndex > hit) SelectedIndex--;
                    }
                    break;
                }
            }
        }
    }
}
