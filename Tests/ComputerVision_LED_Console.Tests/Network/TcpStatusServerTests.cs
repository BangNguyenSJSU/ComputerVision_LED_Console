using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Network;
using ComputerVision_LED_Console.Services;

namespace ComputerVision_LED_Console.Tests.Network;

public class TcpStatusServerTests : IDisposable
{
    private readonly TcpStatusServer _server;
    private readonly StatusService _status;
    private readonly int _port;

    public TcpStatusServerTests()
    {
        _port = PickFreeLoopbackPort();
        _status = new StatusService();
        _server = new TcpStatusServer(new NetworkConfig
        {
            TcpEnabled = true,
            TcpBindAddress = "127.0.0.1",
            TcpPort = _port,
        }, _status);
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    private static int PickFreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task ConnectedClient_ReceivesJsonLine_OnStatusUpdate()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _port);

        // Give the accept loop a moment to register the client before we publish.
        await Task.Delay(200);

        var seed = new SystemStatus { CameraIndex = 7, FrameWidth = 640 };
        seed.Leds.Add(new DetectionResult { MarkerId = 2, Status = LedStatus.On, Brightness = 200 });
        _status.Update(seed);

        using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        string? line = await reader.ReadLineAsync(cts.Token);

        Assert.NotNull(line);
        Assert.Contains("\"cameraIndex\":7", line);
        Assert.Contains("\"status\":\"On\"", line);
    }

    [Fact]
    public async Task MultipleUpdates_ProduceMultipleLines()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _port);
        await Task.Delay(200);

        _status.Update(new SystemStatus { CameraIndex = 1 });
        _status.Update(new SystemStatus { CameraIndex = 2 });

        using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        string? line1 = await reader.ReadLineAsync(cts.Token);
        string? line2 = await reader.ReadLineAsync(cts.Token);

        Assert.Contains("\"cameraIndex\":1", line1);
        Assert.Contains("\"cameraIndex\":2", line2);
    }

    [Fact]
    public void Stop_ClearsIsRunning()
    {
        Assert.True(_server.IsRunning);

        _server.Stop();

        Assert.False(_server.IsRunning);
    }
}
