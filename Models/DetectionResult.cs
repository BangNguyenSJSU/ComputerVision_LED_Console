using System;

namespace ComputerVision_LED_Console.Models
{
    public class DetectionResult
    {
        public int MarkerId { get; set; }
        public LedStatus Status { get; set; }
        public double Brightness { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}
