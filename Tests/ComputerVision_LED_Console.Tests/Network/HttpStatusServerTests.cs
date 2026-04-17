using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Network;
using ComputerVision_LED_Console.Services;

namespace ComputerVision_LED_Console.Tests.Network;

public class HttpStatusServerTests : IDisposable
{
    private readonly HttpStatusServer _server;
    private readonly StatusService _status;
    private readonly int _port;

    public HttpStatusServerTests()
    {
        _port = PickFreeLoopbackPort();
        _status = new StatusService();
        var config = new NetworkConfig
        {
            HttpEnabled = true,
            HttpBindAddress = "127.0.0.1",
            HttpPort = _port,
        };
        _server = new HttpStatusServer(config, _status);
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

    private string BaseUrl => $"http://127.0.0.1:{_port}";

    [Fact]
    public async Task Get_Status_ReturnsJsonOfLatest()
    {
        var seed = new SystemStatus
        {
            TimestampUtc = new DateTime(2026, 4, 17, 12, 0, 0, DateTimeKind.Utc),
            CameraIndex = 3,
            FrameWidth = 1920,
            FrameHeight = 1080,
            FramesPerSecond = 60,
        };
        seed.Leds.Add(new DetectionResult { MarkerId = 1, Status = LedStatus.On, Brightness = 200 });
        _status.Update(seed);

        using var client = new HttpClient();
        var response = await client.GetAsync($"{BaseUrl}/status");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        string body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"cameraIndex\":3", body);
        Assert.Contains("\"frameWidth\":1920", body);
        Assert.Contains("\"status\":\"On\"", body);
    }

    [Fact]
    public async Task Get_UnknownPath_Returns404()
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"{BaseUrl}/nope");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Status_Returns405()
    {
        using var client = new HttpClient();
        var response = await client.PostAsync(
            $"{BaseUrl}/status",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public void Stop_ClearsIsRunning()
    {
        Assert.True(_server.IsRunning);

        _server.Stop();

        Assert.False(_server.IsRunning);
    }
}
