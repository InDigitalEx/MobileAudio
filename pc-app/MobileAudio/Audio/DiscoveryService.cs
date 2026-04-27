using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MobileAudio.Helpers;

namespace MobileAudio.Audio;

/// <summary>
/// Immutable representation of a discovered network device.
/// </summary>
public sealed record DiscoveredDevice(
    string DeviceType,
    string DeviceName,
    string IpAddress,
    int AudioPort,
    DateTime LastSeen
);

/// <summary>
/// Broadcasts and listens for device-discovery messages on the local network.
/// </summary>
public sealed class DiscoveryService : IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _broadcastTask;

    private readonly int _discoveryPort;
    private readonly int _audioPort;
    private readonly string _deviceName;
    private const string DeviceType = "MobileAudioPC";
    private static readonly TimeSpan StaleTimeout = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<string, DiscoveredDevice> _discoveredDevices = new();

    public bool IsRunning => _cts is { IsCancellationRequested: false };
    public event EventHandler<List<DiscoveredDevice>>? DevicesUpdated;

    public DiscoveryService(int discoveryPort = 5001, int audioPort = 5000)
    {
        _discoveryPort = discoveryPort;
        _audioPort = audioPort;
        _deviceName = Environment.MachineName;
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _udpClient = new UdpClient();
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _discoveryPort));
        _udpClient.EnableBroadcast = true;

        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        _broadcastTask = Task.Run(() => BroadcastLoop(_cts.Token));
    }

    public async Task StopAsync()
    {
        if (_cts == null) return;

        _cts.Cancel();
        _udpClient?.Close();

        if (_listenTask != null)
            await _listenTask.ConfigureAwait(false);
        if (_broadcastTask != null)
            await _broadcastTask.ConfigureAwait(false);

        _udpClient?.Dispose();
        _udpClient = null;
        _cts.Dispose();
        _cts = null;
    }

    public void Stop()
    {
        _ = StopAsync();
    }

    public List<DiscoveredDevice> GetDiscoveredPhones()
    {
        var cutoff = DateTime.Now.AddSeconds(-10);
        return _discoveredDevices.Values
            .Where(d => d.DeviceType == "MobileAudioPhone" && d.LastSeen > cutoff)
            .OrderBy(d => d.DeviceName)
            .ToList();
    }

    private async Task BroadcastLoop(CancellationToken token)
    {
        var localIp = NetworkHelper.GetLocalIpAddress();
        var message = $"HELLO|{DeviceType}|{_deviceName}|{localIp}|{_audioPort}";
        var bytes = Encoding.UTF8.GetBytes(message);

        while (!token.IsCancellationRequested)
        {
            try
            {
                var broadcastEp = new IPEndPoint(IPAddress.Broadcast, _discoveryPort);
                _udpClient?.Send(bytes, bytes.Length, broadcastEp);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiscoveryService] Broadcast failed: {ex.Message}");
            }

            CleanStaleDevices();

            try
            {
                await Task.Delay(2000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_udpClient == null) continue;
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = await _udpClient.ReceiveAsync(token);
                var message = Encoding.UTF8.GetString(data.Buffer).Trim();
                ProcessMessage(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiscoveryService] Listen error: {ex.Message}");
            }
        }
    }

    private void ProcessMessage(string message)
    {
        if (!message.StartsWith("HELLO|")) return;

        var parts = message.Split('|');
        if (parts.Length < 5) return;

        var deviceType = parts[1];
        if (deviceType == DeviceType) return; // Ignore ourselves

        var deviceName = parts[2];
        var ip = parts[3];
        var port = int.TryParse(parts[4], out var p) ? p : 5000;

        var device = new DiscoveredDevice(
            DeviceType: deviceType,
            DeviceName: deviceName,
            IpAddress: ip,
            AudioPort: port,
            LastSeen: DateTime.Now
        );

        _discoveredDevices[ip] = device;
        NotifyDevicesUpdated();
    }

    private void CleanStaleDevices()
    {
        var cutoff = DateTime.Now.AddSeconds(-10);
        var staleKeys = _discoveredDevices
            .Where(kv => kv.Value.LastSeen < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in staleKeys)
            _discoveredDevices.TryRemove(key, out _);

        if (staleKeys.Count > 0)
            NotifyDevicesUpdated();
    }

    private void NotifyDevicesUpdated()
    {
        DevicesUpdated?.Invoke(this, GetDiscoveredPhones());
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
        _cts?.Dispose();
    }
}

