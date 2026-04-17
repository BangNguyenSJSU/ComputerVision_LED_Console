namespace ComputerVision_LED_Console.Config
{
    public class NetworkConfig
    {
        public bool HttpEnabled { get; set; } = false;
        public string HttpBindAddress { get; set; } = "127.0.0.1";
        public int HttpPort { get; set; } = 18080;

        public bool TcpEnabled { get; set; } = false;
        public string TcpBindAddress { get; set; } = "127.0.0.1";
        public int TcpPort { get; set; } = 9090;

        public bool TcpBinaryEnabled { get; set; } = false;
        public string TcpBinaryBindAddress { get; set; } = "127.0.0.1";
        public int TcpBinaryPort { get; set; } = 9091;
    }
}
