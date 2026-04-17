namespace ComputerVision_LED_Console.Config
{
    public class CameraConfig
    {
        public int DeviceIndex { get; set; } = 0;
        public int FrameWidth { get; set; } = 1920;
        public int FrameHeight { get; set; } = 1080;
        public int TargetFps { get; set; } = 60;
        public int BufferSize { get; set; } = 1;
        public string FourCC { get; set; } = "MJPG";
        public int MaxProbeIndex { get; set; } = 5;
    }
}
