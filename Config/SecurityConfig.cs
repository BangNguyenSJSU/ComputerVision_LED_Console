namespace ComputerVision_LED_Console.Config
{
    public class SecurityConfig
    {
        public bool LockEnabled { get; set; } = true;
        public string PasswordHash { get; set; } = "";
        public string PasswordSalt { get; set; } = "";
        public int PasswordIterations { get; set; } = 100_000;
    }
}
