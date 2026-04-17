using System;
using ComputerVision_LED_Console.Models;

namespace ComputerVision_LED_Console.Services
{
    public class StatusService
    {
        private readonly object _gate = new();
        private SystemStatus _latest = new();

        public event EventHandler<SystemStatus>? StatusUpdated;

        // Update takes ownership of `status`: callers must construct a fresh SystemStatus
        // each call and not mutate it afterwards. GetLatest clones on read, but StatusUpdated
        // hands subscribers the same reference Update received — mutating it would race.
        public void Update(SystemStatus status)
        {
            lock (_gate)
            {
                _latest = status;
            }

            // Fire outside the lock so a handler that re-enters StatusService cannot deadlock.
            StatusUpdated?.Invoke(this, status);
        }

        public SystemStatus GetLatest()
        {
            lock (_gate)
            {
                return Clone(_latest);
            }
        }

        private static SystemStatus Clone(SystemStatus source)
        {
            var copy = new SystemStatus
            {
                TimestampUtc = source.TimestampUtc,
                CameraIndex = source.CameraIndex,
                FrameWidth = source.FrameWidth,
                FrameHeight = source.FrameHeight,
                FramesPerSecond = source.FramesPerSecond,
            };

            foreach (var led in source.Leds)
            {
                copy.Leds.Add(new DetectionResult
                {
                    MarkerId = led.MarkerId,
                    Status = led.Status,
                    Brightness = led.Brightness,
                    TimestampUtc = led.TimestampUtc,
                });
            }

            return copy;
        }
    }
}
