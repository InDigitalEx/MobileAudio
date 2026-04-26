using System;
using System.Net;
using System.Net.Sockets;
using MobileAudio.Models;

namespace MobileAudio.Audio;

public class UdpStreamer : IDisposable
{
    private UdpClient? _udpClient;
    private IPEndPoint? _targetEndPoint;
    private readonly AudioSettings _settings;
    private uint _sequenceNumber;
    private bool _running;
    private int _framesSent;
    private DateTime _lastLogTime;

    public bool IsStreaming => _running;
    public event EventHandler? Started;
    public event EventHandler? Stopped;

    public UdpStreamer(AudioSettings settings)
    {
        _settings = settings;
    }

    public void Start(string targetIp, int port)
    {
        if (_running) return;
        try
        {
            _targetEndPoint = new IPEndPoint(IPAddress.Parse(targetIp), port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UdpStreamer] ERROR: Invalid IP address '{targetIp}': {ex.Message}");
            throw;
        }
        _udpClient = new UdpClient();
        _udpClient.Client.SendBufferSize = _settings.BufferSize * 10;
        _sequenceNumber = 0;
        _framesSent = 0;
        _lastLogTime = DateTime.Now;
        _running = true;
        Started?.Invoke(this, EventArgs.Empty);
        Console.WriteLine($"[UdpStreamer] Started streaming to {targetIp}:{port}");
        Console.WriteLine($"[UdpStreamer] Target endpoint: {_targetEndPoint}");
    }

    public void SendFrame(byte[] frame)
    {
        if (!_running || _udpClient == null || _targetEndPoint == null) return;

        var packet = new byte[4 + frame.Length];
        var seqBytes = BitConverter.GetBytes(_sequenceNumber++);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(seqBytes);
        seqBytes.CopyTo(packet, 0);
        frame.CopyTo(packet, 4);

        try
        {
            _udpClient.Send(packet, packet.Length, _targetEndPoint);
            _framesSent++;

            if (_framesSent <= 5 || _framesSent % 100 == 0)
            {
                Console.WriteLine($"[UdpStreamer] Sent frame #{_framesSent}, seq={_sequenceNumber}, size={packet.Length}, target={_targetEndPoint}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UdpStreamer] Send error: {ex.Message}");
        }
    }

    public void Stop()
    {
        Console.WriteLine($"[UdpStreamer] Stopping. Total frames sent: {_framesSent}");
        _running = false;
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
    }
}
