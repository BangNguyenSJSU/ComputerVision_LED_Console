using ComputerVision_LED_Console.Models;

namespace ComputerVision_LED_Console.Services
{
    public class StatusService
    {
        private readonly object _gate = new object();
        private SystemStatus _latest = new SystemStatus();

        public void Update(SystemStatus status)
        {
            lock (_gate)
            {
                _latest = status;
            }
        }

        public SystemStatus GetLatest()
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }
}
