using System;
using System.Collections.Generic;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;

namespace ComputerVision_LED_Console.Vision
{
    public class LedDetector : ILedDetector
    {
        private readonly DetectionConfig _config;
        private readonly List<RoiConfig> _rois = new List<RoiConfig>();

        public LedDetector(DetectionConfig config)
        {
            _config = config;
        }

        public IReadOnlyList<RoiConfig> Rois => _rois;

        public void AutoDetect(FrameData frame) => throw new NotImplementedException();
        public DetectionResult Evaluate(FrameData frame, RoiConfig roi) => throw new NotImplementedException();
        public IReadOnlyList<DetectionResult> EvaluateAll(FrameData frame) => throw new NotImplementedException();
    }
}
