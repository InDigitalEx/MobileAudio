using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using MobileAudio.Helpers;
using MobileAudio.Models;

namespace MobileAudio.Audio;

/// <summary>
/// Streams audio frames via UDP to a target endpoint.
/// </summary>
public sealed class UdpStreamer : IDisposable
{
    private UdpClient? _udpClient;
    private IPEndPoint? _targetEndPoint;
    private readonly AudioSettings _settings;
    private uint _sequenceNumber;
    private bool _running;
    private int _framesSent;

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

        if (!IPAddress.TryParse(targetIp, out var ipAddress))
        {
            throw new ArgumentException($"Invalid IP address: '{targetIp}'", nameof(targetIp));
        }

        _targetEndPoint = new IPEndPoint(ipAddress, port);
        _udpClient = new UdpClient
        {
            Client =
            {
                SendBufferSize = _settings.BufferSize * 10
            }
        };
        _sequenceNumber = 0;
        _framesSent = 0;
        _running = true;

        Started?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine($"[UdpStreamer] Started streaming to {targetIp}:{port}");
    }

    public void SendFrame(byte[] frame)
    {
        if (!_running || _udpClient == null || _targetEndPoint == null) return;

        var packet = new byte[4 + frame.Length];
        packet.WriteUInt32BigEndian(_sequenceNumber++);
        frame.CopyTo(packet, 4);

        try
        {
            _udpClient.Send(packet, packet.Length, _targetEndPoint);
            _framesSent++;

            if (_framesSent <= 5 || _framesSent % 100 == 0)
            {
                Debug.WriteLine($"[UdpStreamer] Sent frame #{_framesSent}, seq={_sequenceNumber}, size={packet.Length}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UdpStreamer] Send error: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (!_running) return;

        Debug.WriteLine($"[UdpStreamer] Stopping. Total frames sent: {_framesSent}");
        _running = false;
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        _targetEndPoint = null;
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
    }
}

