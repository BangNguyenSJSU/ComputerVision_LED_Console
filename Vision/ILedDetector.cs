using System.Collections.Generic;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Models;

namespace ComputerVision_LED_Console.Vision
{
    public interface ILedDetector
    {
        IReadOnlyList<RoiConfig> Rois { get; }

        void AutoDetect(FrameData frame);
        DetectionResult Evaluate(FrameData frame, RoiConfig roi);
        IReadOnlyList<DetectionResult> EvaluateAll(FrameData frame);
    }
}
