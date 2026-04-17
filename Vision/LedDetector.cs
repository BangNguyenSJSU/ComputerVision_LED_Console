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
        private readonly List<RoiConfig> _rois = new();
        private int _nextId = 1;

        public LedDetector(DetectionConfig config)
        {
            _config = config;
        }

        public IReadOnlyList<RoiConfig> Rois => _rois;

        // ---- Pure helpers (public static so tests can hit them without building a detector) ----

        public static LedStatus Transition(LedStatus current, double brightness, double onThreshold, double offThreshold)
        {
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

        public int AddRoi(float x, float y, int radius)
        {
            var roi = new RoiConfig
            {
                Id = _nextId++,
                CenterX = x,
                CenterY = y,
                Radius = radius,
                OnThreshold = _config.DefaultOnThreshold,
                OffThreshold = _config.DefaultOffThreshold,
                State = LedStatus.Off,
            };
            _rois.Add(roi);
            return _rois.Count - 1;
        }

        public int AddManualRoi(float x, float y) => AddRoi(x, y, _config.DefaultManualRadius);

        public bool RemoveRoiAt(int index)
        {
            if (index < 0 || index >= _rois.Count) return false;
            _rois.RemoveAt(index);
            return true;
        }

        public void Clear()
        {
            _rois.Clear();
        }

        public void MoveRoi(int index, float x, float y)
        {
            if (index < 0 || index >= _rois.Count) return;
            _rois[index].CenterX = x;
            _rois[index].CenterY = y;
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
                    Brightness = 0,
                    TimestampUtc = frame.TimestampUtc,
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
                Brightness = brightness,
                TimestampUtc = frame.TimestampUtc,
            };
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

            var hsvLow = new Scalar(_config.HueLow, _config.SaturationLow, _config.ValueLow);
            var hsvHigh = new Scalar(_config.HueHigh, _config.SaturationHigh, _config.ValueHigh);

            using var mask = new Mat();
            Cv2.InRange(hsv, hsvLow, hsvHigh, mask);

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
                if (r < _config.MinDetectRadius || r > _config.MaxDetectRadius) continue;

                if (IsDuplicate(center.X, center.Y)) continue;

                AddRoi(center.X, center.Y, r);
            }

            Logger.Info($"Detected {_rois.Count} yellow LED(s).");
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
