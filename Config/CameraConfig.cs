namespace ComputerVision_LED_Console.Config
{
    public class CameraConfig
    {
        public int DeviceIndex { get; set; } = 0;
        public int FrameWidth { get; set; } = 640;
        public int FrameHeight { get; set; } = 480;
        public int TargetFps { get; set; } = 240;
        public int BufferSize { get; set; } = 1;
        public string FourCC { get; set; } = "MJPG";
        public int MaxProbeIndex { get; set; } = 5;
        public int DisplayMaxWidth { get; set; } = 1280;

        // Exposure is in DSHOW log2-seconds units (typical Logitech range -11..-1).
        // AutoExposure uses DSHOW sentinel values: 0.75 = auto, 0.25 = manual.
        public bool AutoExposure { get; set; } = true;
        public double InitialExposure { get; set; } = -6.0;
        public double ExposureMin { get; set; } = -11.0;
        public double ExposureMax { get; set; } = -1.0;
        public double ExposureStep { get; set; } = 1.0;

        public double InitialZoomFactor { get; set; } = 1.0;
        public double MinZoomFactor { get; set; } = 1.0;
        public double MaxZoomFactor { get; set; } = 5.0;
        public double ZoomStep { get; set; } = 0.25;
    }
}
