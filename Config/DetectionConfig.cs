namespace ComputerVision_LED_Console.Config
{
    public class DetectionConfig
    {
        public double DefaultOnThreshold { get; set; } = 24.0;
        public double DefaultOffThreshold { get; set; } = 12.0;
        public double MinThresholdGap { get; set; } = 2.0;
        public double ThresholdStep { get; set; } = 1.0;
        public double CalibrationMargin { get; set; } = 3.0;

        public int MinDetectRadius { get; set; } = 3;
        public int MaxDetectRadius { get; set; } = 24;
        public int DefaultManualRadius { get; set; } = 6;

        // Yellow HSV range.
        public bool DetectYellow { get; set; } = true;
        public int HueLow { get; set; } = 20;
        public int HueHigh { get; set; } = 35;

        // Red wraps the hue circle, so it needs two sub-ranges combined with OR.
        public bool DetectRed { get; set; } = true;
        public int RedHueLow1 { get; set; } = 0;
        public int RedHueHigh1 { get; set; } = 10;
        public int RedHueLow2 { get; set; } = 170;
        public int RedHueHigh2 { get; set; } = 180;

        // Shared saturation and value bounds for every enabled color band.
        public int SaturationLow { get; set; } = 100;
        public int SaturationHigh { get; set; } = 255;
        public int ValueLow { get; set; } = 150;
        public int ValueHigh { get; set; } = 255;
    }
}
