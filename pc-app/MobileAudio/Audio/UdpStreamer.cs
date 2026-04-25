using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MobileAudio.Models;

namespace MobileAudio.Audio;

public class UdpStreamer : IDisposable
{
    private UdpClient? _udpClient;
    private IPEndPoint? _targetEndPoint;
    private readonly AudioSettings _settings;
    private uint _sequenceNumber;
    private bool _running;
    private Thread? _streamThread;
    private readonly AudioCapture _audioCapture;

    public bool IsStreaming => _running;
    public event EventHandler? Started;
    public event EventHandler? Stopped;

    public UdpStreamer(AudioSettings settings, AudioCapture audioCapture)
    {
        _settings = settings;
        _audioCapture = audioCapture;
    }

    public void Start(string targetIp, int port)
    {
        if (_running) return;
        _targetEndPoint = new IPEndPoint(IPAddress.Parse(targetIp), port);
        _udpClient = new UdpClient();
        _udpClient.Client.SendBufferSize = _settings.BufferSize * 100;
        _audioCapture.Start();
        _running = true;
        _streamThread = new Thread(StreamLoop) { IsBackground = true };
        _streamThread.Start();
        Started?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        _running = false;
        _audioCapture.Stop();
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    private void StreamLoop()
    {
        var sleepMs = _settings.FrameDurationMs;
        while (_running)
        {
            var frame = _audioCapture.ReadFrame();
            if (frame == null)
            {
                Thread.Sleep(1);
                continue;
            }

            var packet = new byte[4 + frame.Length];
            BitConverter.GetBytes(_sequenceNumber++).CopyTo(packet, 0);
            frame.CopyTo(packet, 4);

            try
            {
                _udpClient?.Send(packet, packet.Length, _targetEndPoint);
            }
            catch
            {
                // ignore send errors
            }

            Thread.Sleep(sleepMs / 2);
        }
    }

    public void Dispose()
    {
        Stop();
        _udpClient?.Dispose();
    }
}

