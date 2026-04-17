using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Services;
using ComputerVision_LED_Console.Utilities;

namespace ComputerVision_LED_Console.Network
{
    public class TcpStatusServer : IStatusPublisher
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly NetworkConfig _config;
        private readonly StatusService _status;
        private readonly List<TcpClient> _clients = new();
        private readonly object _clientsLock = new();

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;

        public TcpStatusServer(NetworkConfig config, StatusService status)
        {
            _config = config;
            _status = status;
        }

        public bool IsRunning { get; private set; }

        public void Start()
        {
            if (IsRunning) return;

            var addr = IPAddress.Parse(_config.TcpBindAddress);
            var listener = new TcpListener(addr, _config.TcpPort);

            try
            {
                listener.Start();
            }
            catch (SocketException ex)
            {
                Logger.Error($"TCP status server could not bind to {addr}:{_config.TcpPort}: {ex.Message}. Capture will continue without TCP.");
                return;
            }

            _listener = listener;
            _cts = new CancellationTokenSource();
            _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
            _status.StatusUpdated += OnStatusUpdated;

            IsRunning = true;
            Logger.Info($"TCP status server listening on {addr}:{_config.TcpPort}");
        }

        private async Task AcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener!.AcceptTcpClientAsync(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (SocketException) { break; }
                catch (ObjectDisposedException) { break; }

                lock (_clientsLock)
                {
                    _clients.Add(client);
                }
                Logger.Info($"TCP client connected: {client.Client.RemoteEndPoint}");
            }
        }

        private void OnStatusUpdated(object? sender, SystemStatus status)
        {
            string json = JsonSerializer.Serialize(status, JsonOptions);
            byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");

            List<TcpClient> snapshot;
            lock (_clientsLock)
            {
                snapshot = new List<TcpClient>(_clients);
            }

            List<TcpClient>? dead = null;
            foreach (var client in snapshot)
            {
                try
                {
                    if (!client.Connected)
                    {
                        (dead ??= new List<TcpClient>()).Add(client);
                        continue;
                    }
                    client.GetStream().Write(bytes, 0, bytes.Length);
                }
                catch
                {
                    (dead ??= new List<TcpClient>()).Add(client);
                }
            }

            if (dead is not null)
            {
                lock (_clientsLock)
                {
                    foreach (var d in dead)
                    {
                        _clients.Remove(d);
                        try { d.Close(); } catch { }
                    }
                }
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            _status.StatusUpdated -= OnStatusUpdated;

            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            try { _acceptTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }

            lock (_clientsLock)
            {
                foreach (var c in _clients)
                {
                    try { c.Close(); } catch { }
                }
                _clients.Clear();
            }

            _cts?.Dispose();
            _cts = null;
            _listener = null;
            _acceptTask = null;
            IsRunning = false;
        }

        public void Dispose() => Stop();
    }
}
