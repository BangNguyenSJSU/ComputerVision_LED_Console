using System;
using System.Collections.Generic;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console
{
    internal class Program
    {
        const string WindowName = "LED Detection";

        static AppConfig _config = null!;
        static LedDetector _detector = null!;
        static int DragIndex = -1;
        static int SelectedIndex = -1;
        static double DisplayScale = 1.0;

        static void Main(string[] args)
        {
            _config = new AppConfig();
            _detector = new LedDetector(_config.Detection);

            using var camera = new OpenCvCameraSource(_config.Camera);
            if (!camera.Open())
            {
                return;
            }

            DisplayScale = camera.FrameWidth > 1280 ? 1280.0 / camera.FrameWidth : 1.0;

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

                using Mat displayFrame = frame.Clone();

                for (int i = 0; i < _detector.Rois.Count; i++)
                {
                    var roi = _detector.Rois[i];
                    var result = results[i];

                    Scalar color = result.Status == LedStatus.On
                        ? new Scalar(0, 255, 0)
                        : new Scalar(0, 0, 255);

                    int thickness = (i == SelectedIndex) ? 3 : 2;
                    Cv2.Circle(displayFrame, (int)roi.CenterX, (int)roi.CenterY, roi.Radius, color, thickness);

                    string stateText = result.Status.ToString().ToUpperInvariant();
                    string label = (i == SelectedIndex)
                        ? $"{stateText} ({result.Brightness:F0}) [on>={roi.OnThreshold:F0} off<={roi.OffThreshold:F0}]"
                        : $"{stateText} ({result.Brightness:F0})";

                    Point labelPos = new Point(
                        (int)roi.CenterX - roi.Radius,
                        (int)roi.CenterY - roi.Radius - 6);
                    Cv2.PutText(
                        displayFrame,
                        label,
                        labelPos,
                        HersheyFonts.HersheySimplex,
                        0.5,
                        color,
                        2);
                }

                Cv2.PutText(
                    displayFrame,
                    $"LEDs: {_detector.Rois.Count}",
                    new Point(20, 35),
                    HersheyFonts.HersheySimplex,
                    0.8,
                    new Scalar(0, 0, 0),
                    2);

                Cv2.PutText(
                    displayFrame,
                    "Q=quit  R=rescan  L-click=add/drag  R-click=delete  C=calibrate  []=On -/+  ;'=Off -/+",
                    new Point(20, 70),
                    HersheyFonts.HersheySimplex,
                    0.55,
                    new Scalar(0, 0, 0),
                    1);

                if (SelectedIndex >= 0 && SelectedIndex < _detector.Rois.Count
                    && _detector.Rois[SelectedIndex].CalibrationPhase == 1)
                {
                    Cv2.PutText(
                        displayFrame,
                        $"Calibrating marker #{SelectedIndex}: turn LED OFF, press C again",
                        new Point(20, 100),
                        HersheyFonts.HersheySimplex,
                        0.6,
                        new Scalar(0, 0, 255),
                        2);
                }

                // Downscale for display if the frame is larger than 1280 wide,
                // so rendering doesn't back-pressure the capture pipeline.
                if (DisplayScale < 1.0)
                {
                    using var shown = new Mat();
                    Cv2.Resize(displayFrame, shown, new Size(), DisplayScale, DisplayScale, InterpolationFlags.Area);
                    Cv2.ImShow(WindowName, shown);
                }
                else
                {
                    Cv2.ImShow(WindowName, displayFrame);
                }

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
