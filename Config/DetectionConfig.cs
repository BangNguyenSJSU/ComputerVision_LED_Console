namespace ComputerVision_LED_Console.Config
{
    public class DetectionConfig
    {
        public double DefaultOnThreshold { get; set; } = 150.0;
        public double DefaultOffThreshold { get; set; } = 120.0;
        public double MinThresholdGap { get; set; } = 5.0;
        public double ThresholdStep { get; set; } = 5.0;
        public double CalibrationMargin { get; set; } = 10.0;

        public int MinDetectRadius { get; set; } = 4;
        public int MaxDetectRadius { get; set; } = 60;
        public int DefaultManualRadius { get; set; } = 20;

        public int HueLow { get; set; } = 20;
        public int HueHigh { get; set; } = 35;
        public int SaturationLow { get; set; } = 100;
        public int SaturationHigh { get; set; } = 255;
        public int ValueLow { get; set; } = 150;
        public int ValueHigh { get; set; } = 255;
    }
}
