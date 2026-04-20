using System.Collections.Generic;

namespace ComputerVision_LED_Console.Config
{
    public class ConfigSnapshot
    {
        public int Version { get; set; } = 1;
        public AppConfig App { get; set; } = new AppConfig();
        public List<MarkerSnapshot> Markers { get; set; } = new List<MarkerSnapshot>();
    }
}
