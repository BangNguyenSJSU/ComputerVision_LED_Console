using System.Collections.Generic;
using ComputerVision_LED_Console.Camera;
using ComputerVision_LED_Console.Models;

namespace ComputerVision_LED_Console.Vision
{
    public interface ILedDetector
    {
        IReadOnlyList<RoiConfig> Rois { get; }

        void AutoDetect(FrameData frame);
        IReadOnlyList<DetectionResult> EvaluateAll(FrameData frame);

        int AddRoi(float x, float y, int radius);
        int AddManualRoi(float x, float y);
        bool RemoveRoiAt(int index);
        void Clear();
        void MoveRoi(int index, float x, float y);
        int FindRoiAt(float x, float y);
        void TuneOnThreshold(int index, double delta);
        void TuneOffThreshold(int index, double delta);
        void ToggleCalibrate(int index);
    }
}
