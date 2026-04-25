using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MobileAudioPC.Audio;

public class DiscoveryService : IDisposable
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private readonly int _discoveryPort;
    private readonly int _audioPort;
    private Task? _listenTask;
    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    public DiscoveryService(int discoveryPort = 5001, int audioPort = 5000)
    {
        _discoveryPort = discoveryPort;
        _audioPort = audioPort;
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _udpClient = new UdpClient(_discoveryPort);
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
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

    private void ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (_udpClient == null) continue;
                var remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                var data = _udpClient.Receive(ref remoteEndPoint);
                var message = Encoding.UTF8.GetString(data);
                if (message.Trim() == "DISCOVER_MOBILE_AUDIO")
                {
                    var response = $"MOBILE_AUDIO_PC|{_audioPort}|{GetLocalIpAddress()}";
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    _udpClient.Send(responseBytes, responseBytes.Length, remoteEndPoint);
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
            catch
            {
                // ignore other errors
            }
        }
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

