using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ComputerVision_LED_Console.Config;
using ComputerVision_LED_Console.Models;
using ComputerVision_LED_Console.Services;
using ComputerVision_LED_Console.Utilities;

namespace ComputerVision_LED_Console.Network
{
    public class TcpBinaryStatusServer : IStatusPublisher
    {
        private readonly NetworkConfig _config;
        private readonly StatusService _status;
        private readonly List<TcpClient> _clients = new();
        private readonly object _clientsLock = new();
        private readonly HashSet<int> _warnedOverflowIds = new();

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptTask;

        public TcpBinaryStatusServer(NetworkConfig config, StatusService status)
        {
            _config = config;
            _status = status;
        }

        public bool IsRunning { get; private set; }

        public void Start()
        {
            if (IsRunning) return;

            var addr = IPAddress.Parse(_config.TcpBinaryBindAddress);
            var listener = new TcpListener(addr, _config.TcpBinaryPort);

            try
            {
                listener.Start();
            }
            catch (SocketException ex)
            {
                Logger.Error($"TCP binary status server could not bind to {addr}:{_config.TcpBinaryPort}: {ex.Message}. Capture will continue without binary TCP.");
                return;
            }

            _listener = listener;
            _cts = new CancellationTokenSource();
            _acceptTask = Task.Run(() => AcceptLoop(_cts.Token));
            _status.StatusUpdated += OnStatusUpdated;

            IsRunning = true;
            Logger.Info($"TCP binary status server listening on {addr}:{_config.TcpBinaryPort}");
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
                Logger.Info($"TCP binary client connected: {client.Client.RemoteEndPoint}");
            }
        }

        private void OnStatusUpdated(object? sender, SystemStatus status)
        {
            byte[] bytes = Encode(status);

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

        private byte[] Encode(SystemStatus status)
        {
            int leds = status.Leds.Count;
            var output = new byte[1 + Math.Min(leds, 255) * 2];
            byte count = 0;

            foreach (var led in status.Leds)
            {
                if (count == 255) break;

                if (led.MarkerId < 0 || led.MarkerId > 255)
                {
                    if (_warnedOverflowIds.Add(led.MarkerId))
                    {
                        Logger.Warn($"TCP-binary skipping LED with MarkerId={led.MarkerId} (out of 0..255 range).");
                    }
                    continue;
                }

                int offset = 1 + count * 2;
                output[offset] = (byte)led.MarkerId;
                output[offset + 1] = (byte)led.Status;
                count++;
            }

            output[0] = count;

            if (count * 2 + 1 == output.Length)
            {
                return output;
            }

            var trimmed = new byte[1 + count * 2];
            Buffer.BlockCopy(output, 0, trimmed, 0, trimmed.Length);
            return trimmed;
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
