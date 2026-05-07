using System;
using System.Collections.Generic;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Utilities;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Vision
{
    public class LedDetector : ILedDetector
    {
        private readonly DetectionConfig _config;
        private readonly ITimeProvider _time;
        private readonly List<RoiConfig> _rois = new();
        private int _nextId = 1;

        public LedDetector(DetectionConfig config, ITimeProvider time)
        {
            _config = config;
            _time = time;
        }

        public IReadOnlyList<RoiConfig> Rois => _rois;

        // ---- Pure helpers (public static so tests can hit them without building a detector) ----

        public static LedStatus Transition(LedStatus current, double brightness, double onThreshold, double offThreshold)
        {
            // First observation after creation / restore: pick Off or On by the midpoint
            // so a restored marker doesn't stay "Unknown" forever when the current brightness
            // sits inside the hysteresis dead-band.
            if (current == LedStatus.Unknown)
            {
                double mid = (onThreshold + offThreshold) * 0.5;
                return brightness >= mid ? LedStatus.On : LedStatus.Off;
            }
            if (current == LedStatus.Off && brightness >= onThreshold) return LedStatus.On;
            if (current == LedStatus.On && brightness <= offThreshold) return LedStatus.Off;
            return current;
        }

        public static void EnforceThresholdGap(RoiConfig roi, double minGap, bool adjustOn)
        {
            if (roi.OnThreshold - roi.OffThreshold >= minGap) return;
            if (adjustOn) roi.OnThreshold = Math.Min(255, roi.OffThreshold + minGap);
            else roi.OffThreshold = Math.Max(0, roi.OnThreshold - minGap);
        }

        public static Rect ClampRectToFrame(Rect rect, int frameWidth, int frameHeight)
        {
            int x = Math.Max(0, rect.X);
            int y = Math.Max(0, rect.Y);
            int w = Math.Min(rect.Width, frameWidth - x);
            int h = Math.Min(rect.Height, frameHeight - y);
            if (w < 0) w = 0;
            if (h < 0) h = 0;
            return new Rect(x, y, w, h);
        }

        // ---- Mutators ----

        public int AddRoi(float x, float y, int radius) => AddRoi(x, y, radius, LedColor.Unknown);

        public int AddRoi(float x, float y, int radius, LedColor color)
        {
            var roi = new RoiConfig
            {
                Id = _nextId++,
                CenterX = x,
                CenterY = y,
                Radius = radius,
                OnThreshold = _config.DefaultOnThreshold,
                OffThreshold = _config.DefaultOffThreshold,
                Color = color,
                State = LedStatus.Off,
            };
            _rois.Add(roi);
            return _rois.Count - 1;
        }

        public int AddManualRoi(float x, float y) => AddManualRoi(x, y, LedColor.Unknown);

        public int AddManualRoi(float x, float y, LedColor color) => AddRoi(x, y, _config.DefaultManualRadius, color);

        public bool RemoveRoiAt(int index)
        {
            if (index < 0 || index >= _rois.Count) return false;
            _rois.RemoveAt(index);
            return true;
        }

        public void Clear()
        {
            _rois.Clear();
            _nextId = 1;
        }

        public void RestoreMarkers(IEnumerable<MarkerSnapshot> snapshots)
        {
            _rois.Clear();
            int maxId = 0;
            foreach (var m in snapshots)
            {
                _rois.Add(new RoiConfig
                {
                    Id = m.Id,
                    CenterX = m.CenterX,
                    CenterY = m.CenterY,
                    Radius = m.Radius,
                    OnThreshold = m.OnThreshold,
                    OffThreshold = m.OffThreshold,
                    Color = m.Color,
                    State = LedStatus.Unknown,
                });
                if (m.Id > maxId) maxId = m.Id;
            }
            _nextId = maxId + 1;
        }

        public void MoveRoi(int index, float x, float y)
        {
            if (index < 0 || index >= _rois.Count) return;
            _rois[index].CenterX = x;
            _rois[index].CenterY = y;
        }

        public void AdjustRoiRadius(int index, int delta)
        {
            if (index < 0 || index >= _rois.Count) return;
            var roi = _rois[index];
            int next = roi.Radius + delta;
            roi.Radius = Math.Clamp(next, _config.MinRoiRadius, _config.MaxRoiRadius);
        }

        public int FindRoiAt(float x, float y)
        {
            for (int i = 0; i < _rois.Count; i++)
            {
                var roi = _rois[i];
                float dx = roi.CenterX - x;
                float dy = roi.CenterY - y;
                float r = roi.Radius;
                if (dx * dx + dy * dy <= r * r) return i;
            }
            return -1;
        }

        public void TuneOnThreshold(int index, double delta)
        {
            if (index < 0 || index >= _rois.Count) return;
            var roi = _rois[index];
            roi.OnThreshold = Math.Clamp(roi.OnThreshold + delta, 0, 255);
            EnforceThresholdGap(roi, _config.MinThresholdGap, adjustOn: false);
        }

        public void TuneOffThreshold(int index, double delta)
        {
            if (index < 0 || index >= _rois.Count) return;
            var roi = _rois[index];
            roi.OffThreshold = Math.Clamp(roi.OffThreshold + delta, 0, 255);
            EnforceThresholdGap(roi, _config.MinThresholdGap, adjustOn: true);
        }

        public void ToggleCalibrate(int index)
        {
            if (index < 0 || index >= _rois.Count) return;
            var roi = _rois[index];
            if (roi.CalibrationPhase == 0)
            {
                roi.OnThreshold = Math.Max(0, roi.LastBrightness - _config.CalibrationMargin);
                EnforceThresholdGap(roi, _config.MinThresholdGap, adjustOn: false);
                roi.CalibrationPhase = 1;
            }
            else
            {
                roi.OffThreshold = Math.Min(255, roi.LastBrightness + _config.CalibrationMargin);
                EnforceThresholdGap(roi, _config.MinThresholdGap, adjustOn: true);
                roi.CalibrationPhase = 0;
            }
        }

        // ---- Evaluation ----

        public DetectionResult Evaluate(FrameData frame, RoiConfig roi)
        {
            Rect bbox = new(
                (int)(roi.CenterX - roi.Radius),
                (int)(roi.CenterY - roi.Radius),
                roi.Radius * 2,
                roi.Radius * 2);
            Rect safe = ClampRectToFrame(bbox, frame.Frame.Width, frame.Frame.Height);

            if (safe.Width <= 0 || safe.Height <= 0)
            {
                return new DetectionResult
                {
                    MarkerId = roi.Id,
                    Status = LedStatus.Unknown,
                    Color = roi.Color,
                    Brightness = 0,
                    TimestampUtc = _time.UtcNow,
                };
            }

            double brightness;
            using (var roiMat = new Mat(frame.Frame, safe))
            using (var gray = new Mat())
            {
                Cv2.CvtColor(roiMat, gray, ColorConversionCodes.BGR2GRAY);
                brightness = Cv2.Mean(gray).Val0;
            }

            roi.LastBrightness = brightness;
            roi.State = Transition(roi.State, brightness, roi.OnThreshold, roi.OffThreshold);

            return new DetectionResult
            {
                MarkerId = roi.Id,
                Status = roi.State,
                Color = roi.Color,
                Brightness = brightness,
                TimestampUtc = _time.UtcNow,
            };
        }

        public LedColor ClassifyColorAt(FrameData frame, float x, float y)
        {
            int px = (int)Math.Round(x);
            int py = (int)Math.Round(y);
            if (px < 0 || py < 0 || px >= frame.Frame.Width || py >= frame.Frame.Height)
            {
                return LedColor.Unknown;
            }

            using var pixelBgr = new Mat(frame.Frame, new Rect(px, py, 1, 1));
            using var pixelHsv = new Mat();
            Cv2.CvtColor(pixelBgr, pixelHsv, ColorConversionCodes.BGR2HSV);
            var hsv = pixelHsv.At<Vec3b>(0, 0);
            int h = hsv.Item0, s = hsv.Item1, v = hsv.Item2;

            if (s < _config.SaturationLow || s > _config.SaturationHigh) return LedColor.Unknown;
            if (v < _config.ValueLow || v > _config.ValueHigh) return LedColor.Unknown;

            if (_config.DetectYellow && h >= _config.HueLow && h <= _config.HueHigh) return LedColor.Yellow;
            if (_config.DetectRed && ((h >= _config.RedHueLow1 && h <= _config.RedHueHigh1) || (h >= _config.RedHueLow2 && h <= _config.RedHueHigh2))) return LedColor.Red;
            if (_config.DetectGreen && h >= _config.GreenHueLow && h <= _config.GreenHueHigh) return LedColor.Green;

            return LedColor.Unknown;
        }

        public IReadOnlyList<DetectionResult> EvaluateAll(FrameData frame)
        {
            var results = new List<DetectionResult>(_rois.Count);
            foreach (var roi in _rois)
            {
                results.Add(Evaluate(frame, roi));
            }
            return results;
        }

        public void AutoDetect(FrameData frame)
        {
            using var hsv = new Mat();
            Cv2.CvtColor(frame.Frame, hsv, ColorConversionCodes.BGR2HSV);

            int added = 0;
            if (_config.DetectYellow)
            {
                added += RunBand(hsv, LedColor.Yellow, _config.HueLow, _config.HueHigh);
            }
            if (_config.DetectRed)
            {
                added += RunBand(hsv, LedColor.Red, _config.RedHueLow1, _config.RedHueHigh1);
                added += RunBand(hsv, LedColor.Red, _config.RedHueLow2, _config.RedHueHigh2);
            }
            if (_config.DetectGreen)
            {
                added += RunBand(hsv, LedColor.Green, _config.GreenHueLow, _config.GreenHueHigh);
            }

            Logger.Info($"Detected {added} LED(s) (total markers: {_rois.Count}).");
        }

        private int RunBand(Mat hsv, LedColor color, int hLo, int hHi)
        {
            using var band = new Mat();
            Cv2.InRange(
                hsv,
                new Scalar(hLo, _config.SaturationLow, _config.ValueLow),
                new Scalar(hHi, _config.SaturationHigh, _config.ValueHigh),
                band);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
            Cv2.MorphologyEx(band, band, MorphTypes.Open, kernel);

            Cv2.FindContours(
                band,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            int added = 0;
            foreach (var c in contours)
            {
                Cv2.MinEnclosingCircle(c, out Point2f center, out float radius);
                int r = (int)Math.Round(radius);
                if (r < _config.MinDetectRadius || r > _config.MaxDetectRadius) continue;
                if (IsDuplicate(center.X, center.Y)) continue;

                AddRoi(center.X, center.Y, r, color);
                added++;
            }
            return added;
        }

        private bool IsDuplicate(float x, float y)
        {
            foreach (var existing in _rois)
            {
                float dx = existing.CenterX - x;
                float dy = existing.CenterY - y;
                if (dx * dx + dy * dy < existing.Radius * existing.Radius) return true;
            }
            return false;
        }
    }
}
