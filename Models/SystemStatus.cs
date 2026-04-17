using System;
using System.Collections.Generic;

namespace ComputerVision_LED_Console.Models
{
    public class SystemStatus
    {
        public DateTime TimestampUtc { get; set; }
        public int CameraIndex { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public double FramesPerSecond { get; set; }
        public List<DetectionResult> Leds { get; set; } = new List<DetectionResult>();
    }
}
