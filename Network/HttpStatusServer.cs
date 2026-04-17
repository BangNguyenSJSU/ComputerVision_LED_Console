using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Services;
using ComputerVision_LED_Console.Utilities;

namespace ComputerVision_LED_Console.Network
{
    public class HttpStatusServer : IStatusPublisher
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly NetworkConfig _config;
        private readonly StatusService _status;
        private HttpListener? _listener;
        private Task? _acceptTask;
        private CancellationTokenSource? _cts;

        public HttpStatusServer(NetworkConfig config, StatusService status)
        {
            _config = config;
            _status = status;
        }

        public bool IsRunning { get; private set; }

        public void Start()
        {
            if (IsRunning) return;

            string prefix = $"http://{_config.HttpBindAddress}:{_config.HttpPort}/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));

            IsRunning = true;
            Logger.Info($"HTTP status server listening on {prefix}");
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener is { IsListening: true })
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync();
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }

                _ = Task.Run(() => HandleRequest(ctx));
            }
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                if (!string.Equals(ctx.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    ctx.Response.Close();
                    return;
                }

                if (ctx.Request.Url?.AbsolutePath != "/status")
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    ctx.Response.Close();
                    return;
                }

                var latest = _status.GetLatest();
                string json = JsonSerializer.Serialize(latest, JsonOptions);
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                ctx.Response.ContentLength64 = bytes.Length;
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.Close();
            }
            catch (Exception ex)
            {
                Logger.Error($"HTTP request handler failed: {ex.Message}");
                try { ctx.Response.Abort(); } catch { /* best effort */ }
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            try { _acceptTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }

            _cts?.Dispose();
            _cts = null;
            _listener = null;
            _acceptTask = null;
            IsRunning = false;
        }

        public void Dispose() => Stop();
    }
}
