using System;
using OpenCvSharp;

namespace ComputerVision_LED_Console.Camera
{
    public class FrameData : IDisposable
    {
        public Mat Frame { get; }
        public DateTime TimestampUtc { get; }

        public FrameData(Mat frame, DateTime timestampUtc)
        {
            Frame = frame;
            TimestampUtc = timestampUtc;
        }

        public void Dispose() => Frame.Dispose();
    }
}
