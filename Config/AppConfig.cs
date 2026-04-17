namespace ComputerVision_LED_Console.Config
{
    public class AppConfig
    {
        public CameraConfig Camera { get; set; } = new CameraConfig();
        public DetectionConfig Detection { get; set; } = new DetectionConfig();
        public NetworkConfig Network { get; set; } = new NetworkConfig();
    }
}
