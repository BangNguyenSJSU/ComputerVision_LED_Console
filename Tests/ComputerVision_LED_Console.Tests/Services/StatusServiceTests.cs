using System;
using System.Threading;
using System.Threading.Tasks;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Services;

namespace ComputerVision_LED_Console.Tests.Services;

public class StatusServiceTests
{
    [Fact]
    public void Update_FollowedByGetLatest_ReturnsSameData()
    {
        var service = new StatusService();
        var status = new SystemStatus
        {
            TimestampUtc = new DateTime(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc),
            CameraIndex = 1,
            FrameWidth = 1920,
            FrameHeight = 1080,
            FramesPerSecond = 60,
        };
        status.Leds.Add(new DetectionResult { MarkerId = 7, Status = LedStatus.On, Brightness = 180.5 });

        service.Update(status);
        var latest = service.GetLatest();

        Assert.Equal(new DateTime(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc), latest.TimestampUtc);
        Assert.Equal(1, latest.CameraIndex);
        Assert.Single(latest.Leds);
        Assert.Equal(7, latest.Leds[0].MarkerId);
        Assert.Equal(LedStatus.On, latest.Leds[0].Status);
    }

    [Fact]
    public void GetLatest_ReturnsSnapshot_MutatingItDoesNotAffectStore()
    {
        var service = new StatusService();
        var status = new SystemStatus { CameraIndex = 1 };
        status.Leds.Add(new DetectionResult { MarkerId = 1, Status = LedStatus.On });

        service.Update(status);

        var snapshot = service.GetLatest();
        snapshot.CameraIndex = 99;
        snapshot.Leds[0].Status = LedStatus.Off;
        snapshot.Leds.Add(new DetectionResult { MarkerId = 999 });

        var again = service.GetLatest();
        Assert.Equal(1, again.CameraIndex);
        Assert.Single(again.Leds);
        Assert.Equal(LedStatus.On, again.Leds[0].Status);
    }

    [Fact]
    public void StatusUpdated_FiresAfterUpdate_WithTheUpdatedStatus()
    {
        var service = new StatusService();
        SystemStatus? captured = null;
        service.StatusUpdated += (_, s) => captured = s;

        var status = new SystemStatus { CameraIndex = 42 };
        service.Update(status);

        Assert.NotNull(captured);
        Assert.Equal(42, captured!.CameraIndex);
    }

    [Fact]
    public async Task ParallelWritersAndReaders_DoNotThrow()
    {
        var service = new StatusService();
        int iterations = 1000;
        int writers = 4;
        int readers = 4;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var writerTasks = new Task[writers];
        for (int w = 0; w < writers; w++)
        {
            int id = w;
            writerTasks[w] = Task.Run(() =>
            {
                for (int i = 0; i < iterations && !cts.Token.IsCancellationRequested; i++)
                {
                    var s = new SystemStatus { CameraIndex = id };
                    s.Leds.Add(new DetectionResult { MarkerId = i, Status = LedStatus.On });
                    service.Update(s);
                }
            });
        }

        var readerTasks = new Task[readers];
        for (int r = 0; r < readers; r++)
        {
            readerTasks[r] = Task.Run(() =>
            {
                for (int i = 0; i < iterations && !cts.Token.IsCancellationRequested; i++)
                {
                    var s = service.GetLatest();
                    // Touch every field — exception here would indicate a torn read.
                    _ = s.CameraIndex;
                    foreach (var led in s.Leds) _ = led.MarkerId;
                }
            });
        }

        await Task.WhenAll(writerTasks);
        await Task.WhenAll(readerTasks);
    }
}
