using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Network;
using ComputerVision_LED_Console.Services;

namespace ComputerVision_LED_Console.Tests.Network;

public class TcpBinaryStatusServerTests : IDisposable
{
    private readonly TcpBinaryStatusServer _server;
    private readonly StatusService _status;
    private readonly int _port;

    public TcpBinaryStatusServerTests()
    {
        _port = PickFreeLoopbackPort();
        _status = new StatusService();
        _server = new TcpBinaryStatusServer(new NetworkConfig
        {
            TcpBinaryEnabled = true,
            TcpBinaryBindAddress = "127.0.0.1",
            TcpBinaryPort = _port,
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

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken ct)
    {
        var buffer = new byte[length];
        int read = 0;
        while (read < length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(read, length - read), ct);
            if (n == 0) throw new IOException("stream closed before expected bytes arrived");
            read += n;
        }
        return buffer;
    }

    [Fact]
    public async Task ConnectedClient_ReceivesBinaryFrame_OnStatusUpdate()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _port);
        await Task.Delay(200);

        var seed = new SystemStatus();
        seed.Leds.Add(new DetectionResult { MarkerId = 1, Status = LedStatus.On });
        seed.Leds.Add(new DetectionResult { MarkerId = 2, Status = LedStatus.Off });
        _status.Update(seed);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stream = client.GetStream();

        var countBuf = await ReadExactAsync(stream, 1, cts.Token);
        Assert.Equal(2, countBuf[0]);

        var payload = await ReadExactAsync(stream, 4, cts.Token);
        Assert.Equal(1, payload[0]);
        Assert.Equal((byte)LedStatus.On, payload[1]);
        Assert.Equal(2, payload[2]);
        Assert.Equal((byte)LedStatus.Off, payload[3]);
    }

    [Fact]
    public async Task MarkerIdAboveByteMax_IsSkippedAndCountReflectsOnlyValidLeds()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _port);
        await Task.Delay(200);

        var seed = new SystemStatus();
        seed.Leds.Add(new DetectionResult { MarkerId = 1, Status = LedStatus.On });
        seed.Leds.Add(new DetectionResult { MarkerId = 300, Status = LedStatus.Off });
        seed.Leds.Add(new DetectionResult { MarkerId = 7, Status = LedStatus.On });
        _status.Update(seed);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stream = client.GetStream();

        var countBuf = await ReadExactAsync(stream, 1, cts.Token);
        Assert.Equal(2, countBuf[0]);

        var payload = await ReadExactAsync(stream, 4, cts.Token);
        Assert.Equal(1, payload[0]);
        Assert.Equal((byte)LedStatus.On, payload[1]);
        Assert.Equal(7, payload[2]);
        Assert.Equal((byte)LedStatus.On, payload[3]);
    }

    [Fact]
    public async Task NoLeds_EmitsSingleZeroByte()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _port);
        await Task.Delay(200);

        _status.Update(new SystemStatus());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stream = client.GetStream();

        var countBuf = await ReadExactAsync(stream, 1, cts.Token);
        Assert.Equal(0, countBuf[0]);
    }

    [Fact]
    public async Task MultipleUpdates_ProduceMultipleFrames()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, _port);
        await Task.Delay(200);

        var first = new SystemStatus();
        first.Leds.Add(new DetectionResult { MarkerId = 5, Status = LedStatus.On });
        _status.Update(first);

        var second = new SystemStatus();
        second.Leds.Add(new DetectionResult { MarkerId = 9, Status = LedStatus.Off });
        _status.Update(second);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var stream = client.GetStream();

        var frame1 = await ReadExactAsync(stream, 3, cts.Token);
        Assert.Equal(1, frame1[0]);
        Assert.Equal(5, frame1[1]);
        Assert.Equal((byte)LedStatus.On, frame1[2]);

        var frame2 = await ReadExactAsync(stream, 3, cts.Token);
        Assert.Equal(1, frame2[0]);
        Assert.Equal(9, frame2[1]);
        Assert.Equal((byte)LedStatus.Off, frame2[2]);
    }

    [Fact]
    public void Stop_ClearsIsRunning()
    {
        Assert.True(_server.IsRunning);

        _server.Stop();

        Assert.False(_server.IsRunning);
    }
}
