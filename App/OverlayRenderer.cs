using System.Collections.Generic;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Vision;
using OpenCvSharp;

namespace ComputerVision_LED_Console.App
{
    public class OverlayRenderer
    {
        private static readonly Scalar OnColor = new(0, 255, 0);     // green
        private static readonly Scalar OffColor = new(0, 0, 255);    // red
        private static readonly Scalar HudColor = new(0, 0, 0);      // black
        private static readonly Scalar WarnColor = new(0, 0, 255);   // red

        private readonly AppState _state;

        public OverlayRenderer(AppState state)
        {
            _state = state;
        }

        public void RenderAndShow(
            string windowName,
            Mat sourceFrame,
            IReadOnlyList<RoiConfig> rois,
            IReadOnlyList<DetectionResult> results,
            int selectedIndex)
        {
            using Mat displayFrame = sourceFrame.Clone();

            DrawMarkers(displayFrame, rois, results, selectedIndex);
            DrawHud(displayFrame, rois.Count);
            DrawLockedChipIfLocked(displayFrame);
            DrawCalibrationPromptIfActive(displayFrame, rois, selectedIndex);
            Show(windowName, displayFrame);
        }

        private void DrawLockedChipIfLocked(Mat displayFrame)
        {
            if (_state.Unlocked) return;
            int x = displayFrame.Width - 130;
            Cv2.PutText(
                displayFrame,
                "LOCKED",
                new Point(x, 35),
                HersheyFonts.HersheySimplex,
                0.8,
                WarnColor,
                2);
        }

        private static void DrawMarkers(
            Mat displayFrame,
            IReadOnlyList<RoiConfig> rois,
            IReadOnlyList<DetectionResult> results,
            int selectedIndex)
        {
            for (int i = 0; i < rois.Count; i++)
            {
                var roi = rois[i];
                var result = results[i];

                Scalar color = result.Status == LedStatus.On ? OnColor : OffColor;
                int thickness = (i == selectedIndex) ? 3 : 2;
                Cv2.Circle(displayFrame, (int)roi.CenterX, (int)roi.CenterY, roi.Radius, color, thickness);

                string colorLetter = result.Color switch
                {
                    LedColor.Red => "R",
                    LedColor.Yellow => "Y",
                    LedColor.Green => "G",
                    _ => "",
                };
                string stateText = $"{colorLetter}{result.MarkerId}-{result.Status.ToString().ToUpperInvariant()}";
                string label = (i == selectedIndex)
                    ? $"{stateText} ({result.Brightness:F0}) [on>={roi.OnThreshold:F0} off<={roi.OffThreshold:F0}]"
                    : $"{stateText} ({result.Brightness:F0})";

                var labelPos = new Point(
                    (int)roi.CenterX - roi.Radius,
                    (int)roi.CenterY - roi.Radius - 6);
                Cv2.PutText(displayFrame, label, labelPos, HersheyFonts.HersheySimplex, 0.5, color, 2);
            }
        }

        private static void DrawHud(Mat displayFrame, int ledCount)
        {
            Cv2.PutText(
                displayFrame,
                $"LEDs: {ledCount}",
                new Point(20, 35),
                HersheyFonts.HersheySimplex,
                0.8,
                HudColor,
                2);

            Cv2.PutText(
                displayFrame,
                "Q=quit  R=rescan  L-click=add/drag  R-click=delete  C=calibrate  []=On -/+  ;'=Off -/+",
                new Point(20, 70),
                HersheyFonts.HersheySimplex,
                0.55,
                HudColor,
                1);
        }

        private static void DrawCalibrationPromptIfActive(
            Mat displayFrame,
            IReadOnlyList<RoiConfig> rois,
            int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= rois.Count) return;
            if (rois[selectedIndex].CalibrationPhase != 1) return;

            Cv2.PutText(
                displayFrame,
                $"Calibrating marker #{selectedIndex}: turn LED OFF, press C again",
                new Point(20, 100),
                HersheyFonts.HersheySimplex,
                0.6,
                WarnColor,
                2);
        }

        private void Show(string windowName, Mat displayFrame)
        {
            // Downscale for display when the frame is larger than ~1280 wide so
            // rendering doesn't back-pressure the capture pipeline.
            double scale = _state.DisplayScale;
            if (scale < 1.0)
            {
                using var shown = new Mat();
                Cv2.Resize(displayFrame, shown, new Size(), scale, scale, InterpolationFlags.Area);
                Cv2.ImShow(windowName, shown);
            }
            else
            {
                Cv2.ImShow(windowName, displayFrame);
            }
        }
    }
}
