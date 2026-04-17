using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace ComputerVision_LED_Console
{
    internal class Program
    {
        class LedMarker
        {
            public const double DefaultOn = 150.0;
            public const double DefaultOff = 120.0;

            public Point2f Center;
            public int Radius;
            public string State = "OFF";
            public double OnThreshold = DefaultOn;
            public double OffThreshold = DefaultOff;
            public double LastBrightness;
            public int CalibrationPhase;
        }

        static readonly Scalar YellowLow = new Scalar(20, 100, 150);
        static readonly Scalar YellowHigh = new Scalar(35, 255, 255);

        const double ThresholdStep = 5.0;
        const double MinThresholdGap = 5.0;
        const double CalibrationMargin = 10.0;

        const int DefaultManualRadius = 20;
        const int MinDetectRadius = 4;
        const int MaxDetectRadius = 60;

        const string WindowName = "LED Detection";

        static readonly List<LedMarker> Markers = new List<LedMarker>();
        static int DragIndex = -1;
        static int SelectedIndex = -1;
        static double DisplayScale = 1.0;

        static void Main(string[] args)
        {
            int selectedIndex = SelectCamera();
            if (selectedIndex < 0)
            {
                return;
            }

            using var capture = new VideoCapture(selectedIndex, VideoCaptureAPIs.DSHOW);

            if (!capture.IsOpened())
            {
                Console.WriteLine($"Failed to open camera at index {selectedIndex}.");
                return;
            }

            // Request MJPEG so high-res USB webcams fit in USB bandwidth;
            // cap resolution and buffer so a 4K camera does not stall the pipeline.
            capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
            capture.Set(VideoCaptureProperties.FrameWidth, 1920);
            capture.Set(VideoCaptureProperties.FrameHeight, 1080);
            capture.Set(VideoCaptureProperties.Fps, 60);
            capture.Set(VideoCaptureProperties.BufferSize, 1);

            int frameWidth = (int)capture.Get(VideoCaptureProperties.FrameWidth);
            int frameHeight = (int)capture.Get(VideoCaptureProperties.FrameHeight);
            double actualFps = capture.Get(VideoCaptureProperties.Fps);
            DisplayScale = frameWidth > 1280 ? 1280.0 / frameWidth : 1.0;

            Console.WriteLine($"Using camera index {selectedIndex} at {frameWidth}x{frameHeight} @ {actualFps:F1} fps.");
            Console.WriteLine("Controls: Q/ESC=quit | R=rescan | Left-click=add/drag | Right-click=delete");
            Console.WriteLine($"Per-LED thresholds (defaults On={LedMarker.DefaultOn}, Off={LedMarker.DefaultOff}). Left-click an LED to select it.");
            Console.WriteLine("  - C = auto-calibrate selected LED: press while lit, then press again while dark.");
            Console.WriteLine("  - [ / ]  nudge On threshold by 5 (down / up).");
            Console.WriteLine("  - ; / '  nudge Off threshold by 5 (down / up).");
            Console.WriteLine($"  - Minimum gap of {MinThresholdGap} is enforced to prevent flicker.");

            using var frame = new Mat();
            bool initialized = false;

            Cv2.NamedWindow(WindowName);
            Cv2.SetMouseCallback(WindowName, OnMouse);

            while (true)
            {
                capture.Read(frame);

                if (frame.Empty())
                {
                    continue;
                }

                if (!initialized)
                {
                    DetectYellowLeds(frame);
                    initialized = true;
                }

                using Mat displayFrame = frame.Clone();

                for (int i = 0; i < Markers.Count; i++)
                {
                    var m = Markers[i];
                    Rect bbox = new Rect(
                        (int)(m.Center.X - m.Radius),
                        (int)(m.Center.Y - m.Radius),
                        m.Radius * 2,
                        m.Radius * 2);
                    Rect safe = ClampRectToFrame(bbox, displayFrame.Width, displayFrame.Height);
                    if (safe.Width <= 0 || safe.Height <= 0)
                    {
                        continue;
                    }

                    using Mat roiMat = new Mat(displayFrame, safe);
                    using Mat gray = new Mat();
                    Cv2.CvtColor(roiMat, gray, ColorConversionCodes.BGR2GRAY);
                    double brightness = Cv2.Mean(gray).Val0;
                    m.LastBrightness = brightness;

                    if (m.State == "OFF" && brightness >= m.OnThreshold)
                    {
                        m.State = "ON";
                    }
                    else if (m.State == "ON" && brightness <= m.OffThreshold)
                    {
                        m.State = "OFF";
                    }

                    Scalar color = m.State == "ON"
                        ? new Scalar(0, 255, 0)
                        : new Scalar(0, 0, 255);

                    int thickness = (i == SelectedIndex) ? 3 : 2;
                    Cv2.Circle(displayFrame, (int)m.Center.X, (int)m.Center.Y, m.Radius, color, thickness);

                    string label = (i == SelectedIndex)
                        ? $"{m.State} ({brightness:F0}) [on>={m.OnThreshold:F0} off<={m.OffThreshold:F0}]"
                        : $"{m.State} ({brightness:F0})";

                    Point labelPos = new Point(
                        (int)m.Center.X - m.Radius,
                        (int)m.Center.Y - m.Radius - 6);
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
                    $"LEDs: {Markers.Count}",
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

                if (SelectedIndex >= 0 && SelectedIndex < Markers.Count
                    && Markers[SelectedIndex].CalibrationPhase == 1)
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
                    Markers.Clear();
                    DragIndex = -1;
                    SelectedIndex = -1;
                    DetectYellowLeds(frame);
                }

                LedMarker? sel = (SelectedIndex >= 0 && SelectedIndex < Markers.Count)
                    ? Markers[SelectedIndex] : null;

                if (sel != null)
                {
                    if (key == 'c' || key == 'C')
                    {
                        if (sel.CalibrationPhase == 0)
                        {
                            sel.OnThreshold = Math.Max(0, sel.LastBrightness - CalibrationMargin);
                            EnforceThresholdGap(sel, adjustOn: false);
                            sel.CalibrationPhase = 1;
                        }
                        else
                        {
                            sel.OffThreshold = Math.Min(255, sel.LastBrightness + CalibrationMargin);
                            EnforceThresholdGap(sel, adjustOn: true);
                            sel.CalibrationPhase = 0;
                        }
                    }
                    else if (key == ']')
                    {
                        sel.OnThreshold = Math.Min(255, sel.OnThreshold + ThresholdStep);
                        EnforceThresholdGap(sel, adjustOn: false);
                    }
                    else if (key == '[')
                    {
                        sel.OnThreshold = Math.Max(0, sel.OnThreshold - ThresholdStep);
                        EnforceThresholdGap(sel, adjustOn: false);
                    }
                    else if (key == '\'')
                    {
                        sel.OffThreshold = Math.Min(255, sel.OffThreshold + ThresholdStep);
                        EnforceThresholdGap(sel, adjustOn: true);
                    }
                    else if (key == ';')
                    {
                        sel.OffThreshold = Math.Max(0, sel.OffThreshold - ThresholdStep);
                        EnforceThresholdGap(sel, adjustOn: true);
                    }
                }
            }

            capture.Release();
            Cv2.DestroyAllWindows();
        }

        static void DetectYellowLeds(Mat frame)
        {
            using var hsv = new Mat();
            Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);

            using var mask = new Mat();
            Cv2.InRange(hsv, YellowLow, YellowHigh, mask);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);

            Cv2.FindContours(
                mask,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            foreach (var c in contours)
            {
                Cv2.MinEnclosingCircle(c, out Point2f center, out float radius);
                int r = (int)Math.Round(radius);
                if (r < MinDetectRadius || r > MaxDetectRadius)
                {
                    continue;
                }

                bool duplicate = false;
                foreach (var existing in Markers)
                {
                    float dx = existing.Center.X - center.X;
                    float dy = existing.Center.Y - center.Y;
                    if (dx * dx + dy * dy < existing.Radius * existing.Radius)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate)
                {
                    continue;
                }

                Markers.Add(new LedMarker { Center = center, Radius = r });
            }

            Console.WriteLine($"Detected {Markers.Count} yellow LED(s).");
        }

        static void OnMouse(MouseEventTypes @event, int x, int y, MouseEventFlags flags, IntPtr userData)
        {
            float nx = (float)(x / DisplayScale);
            float ny = (float)(y / DisplayScale);

            switch (@event)
            {
                case MouseEventTypes.LButtonDown:
                {
                    int hit = FindMarkerAt(nx, ny);
                    if (hit >= 0)
                    {
                        DragIndex = hit;
                        SelectedIndex = hit;
                    }
                    else
                    {
                        Markers.Add(new LedMarker
                        {
                            Center = new Point2f(nx, ny),
                            Radius = DefaultManualRadius,
                        });
                        DragIndex = Markers.Count - 1;
                        SelectedIndex = Markers.Count - 1;
                    }
                    break;
                }
                case MouseEventTypes.MouseMove:
                    if ((flags & MouseEventFlags.LButton) != 0 && DragIndex >= 0 && DragIndex < Markers.Count)
                    {
                        Markers[DragIndex].Center = new Point2f(nx, ny);
                    }
                    break;
                case MouseEventTypes.LButtonUp:
                    DragIndex = -1;
                    break;
                case MouseEventTypes.RButtonDown:
                {
                    int hit = FindMarkerAt(nx, ny);
                    if (hit >= 0)
                    {
                        Markers.RemoveAt(hit);
                        if (DragIndex == hit) DragIndex = -1;
                        else if (DragIndex > hit) DragIndex--;
                        if (SelectedIndex == hit) SelectedIndex = -1;
                        else if (SelectedIndex > hit) SelectedIndex--;
                    }
                    break;
                }
            }
        }

        static void EnforceThresholdGap(LedMarker m, bool adjustOn)
        {
            if (m.OnThreshold - m.OffThreshold >= MinThresholdGap) return;
            if (adjustOn) m.OnThreshold = Math.Min(255, m.OffThreshold + MinThresholdGap);
            else m.OffThreshold = Math.Max(0, m.OnThreshold - MinThresholdGap);
        }

        static int FindMarkerAt(float x, float y)
        {
            for (int i = 0; i < Markers.Count; i++)
            {
                float dx = Markers[i].Center.X - x;
                float dy = Markers[i].Center.Y - y;
                float r = Markers[i].Radius;
                if (dx * dx + dy * dy <= r * r)
                {
                    return i;
                }
            }
            return -1;
        }

        static int SelectCamera(int maxIndexToProbe = 5)
        {
            Console.WriteLine("Scanning for available cameras...");
            var available = new List<int>();

            for (int i = 0; i <= maxIndexToProbe; i++)
            {
                using var probe = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                if (!probe.IsOpened())
                {
                    continue;
                }

                using var testFrame = new Mat();
                probe.Read(testFrame);
                if (!testFrame.Empty())
                {
                    available.Add(i);
                }
            }

            if (available.Count == 0)
            {
                Console.WriteLine("No cameras detected.");
                return -1;
            }

            Console.WriteLine("Available cameras:");
            for (int i = 0; i < available.Count; i++)
            {
                string hint = available[i] == 0 ? " (typically integrated webcam)" : " (external / secondary)";
                Console.WriteLine($"  [{i}] Camera index {available[i]}{hint}");
            }

            if (available.Count == 1)
            {
                Console.WriteLine("Only one camera found — selecting it automatically.");
                return available[0];
            }

            while (true)
            {
                Console.Write($"Select a camera [0-{available.Count - 1}] (Enter for 0): ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    return available[0];
                }

                if (int.TryParse(input, out int choice) && choice >= 0 && choice < available.Count)
                {
                    return available[choice];
                }

                Console.WriteLine("Invalid selection, try again.");
            }
        }

        static Rect ClampRectToFrame(Rect rect, int frameWidth, int frameHeight)
        {
            int x = Math.Max(0, rect.X);
            int y = Math.Max(0, rect.Y);
            int w = Math.Min(rect.Width, frameWidth - x);
            int h = Math.Min(rect.Height, frameHeight - y);

            if (w < 0) w = 0;
            if (h < 0) h = 0;

            return new Rect(x, y, w, h);
        }
    }
}
