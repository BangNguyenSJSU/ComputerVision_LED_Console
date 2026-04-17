namespace ComputerVision_LED_Console.Vision
{
    public class RoiConfig
    {
        public int Id { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public int Radius { get; set; }
        public double OnThreshold { get; set; }
        public double OffThreshold { get; set; }
    }
}
