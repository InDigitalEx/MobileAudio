using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MobileAudio.Audio;

public class DiscoveredDevice
{
    public string DeviceType { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public int AudioPort { get; set; }
    public DateTime LastSeen { get; set; }
}

public class DiscoveryService : IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private readonly int _discoveryPort;
    private readonly int _audioPort;
    private Task? _listenTask;
    private Task? _broadcastTask;
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    private readonly ConcurrentDictionary<string, DiscoveredDevice> _discoveredDevices = new();
    private readonly string _deviceName;
    private readonly string _deviceType = "MobileAudioPC";

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

    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        _cts?.Dispose();
        _cts = null;
    }

    public List<DiscoveredDevice> GetDiscoveredPhones()
    {
        var cutoff = DateTime.Now.AddSeconds(-10);
        return _discoveredDevices.Values
            .Where(d => d.DeviceType == "MobileAudioPhone" && d.LastSeen > cutoff)
            .OrderBy(d => d.DeviceName)
            .ToList();
    }

    private void BroadcastLoop(CancellationToken token)
    {
        var localIp = GetLocalIpAddress();
        var message = $"HELLO|{_deviceType}|{_deviceName}|{localIp}|{_audioPort}";
        var bytes = Encoding.UTF8.GetBytes(message);

        while (!token.IsCancellationRequested)
        {
            try
            {
                var broadcastEp = new IPEndPoint(IPAddress.Broadcast, _discoveryPort);
                _udpClient?.Send(bytes, bytes.Length, broadcastEp);
            }
            catch { }

            try
            {
                // Clean up stale devices
                var cutoff = DateTime.Now.AddSeconds(-10);
                var stale = _discoveredDevices.Where(kv => kv.Value.LastSeen < cutoff).Select(kv => kv.Key).ToList();
                foreach (var key in stale)
                    _discoveredDevices.TryRemove(key, out _);

                if (stale.Count > 0)
                    NotifyDevicesUpdated();
            }
            catch { }

            Thread.Sleep(2000);
        }
    }

    private void ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_udpClient == null) continue;
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = _udpClient.Receive(ref remoteEndPoint);
                var message = Encoding.UTF8.GetString(data).Trim();

                if (message.StartsWith("HELLO|"))
                {
                    var parts = message.Split('|');
                    if (parts.Length >= 5)
                    {
                        var deviceType = parts[1];
                        var deviceName = parts[2];
                        var ip = parts[3];
                        var port = int.TryParse(parts[4], out var p) ? p : 5000;

                        // Don't add ourselves
                        if (deviceType == _deviceType) continue;

                        var device = new DiscoveredDevice
                        {
                            DeviceType = deviceType,
                            DeviceName = deviceName,
                            IpAddress = ip,
                            AudioPort = port,
                            LastSeen = DateTime.Now
                        };

                        _discoveredDevices[ip] = device;
                        NotifyDevicesUpdated();
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (token.IsCancellationRequested) break;
            }
            catch { }
        }
    }

    private void NotifyDevicesUpdated()
    {
        DevicesUpdated?.Invoke(this, GetDiscoveredPhones());
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
        _cts?.Dispose();
    }
}

