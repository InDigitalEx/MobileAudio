using System;
using System.Threading;
using NAudio.Wave;

namespace MobileAudioPC.Audio;

public class AudioCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _sourceBuffer;
    private MediaFoundationResampler? _resampler;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _bitsPerSample;
    private readonly int _frameSize;
    private readonly Thread _readThread;
    private bool _running;
    public event EventHandler? CaptureStopped;
    public event EventHandler? CaptureStarted;

    public AudioCapture(int sampleRate = 48000, int channels = 2, int bitsPerSample = 16, int frameDurationMs = 10)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _bitsPerSample = bitsPerSample;
        _frameSize = sampleRate * channels * (bitsPerSample / 8) * frameDurationMs / 1000;
        _readThread = new Thread(ReadLoop) { IsBackground = true };
    }

    public void Start()
    {
        if (_running) return;
        _capture = new WasapiLoopbackCapture();
        _sourceBuffer = new BufferedWaveProvider(_capture.WaveFormat)
        {
            BufferLength = _capture.WaveFormat.AverageBytesPerSecond * 2,
            DiscardOnBufferOverflow = true
        };

        var targetFormat = new WaveFormat(_sampleRate, _bitsPerSample, _channels);
        if (!_capture.WaveFormat.Equals(targetFormat))
        {
            _resampler = new MediaFoundationResampler(_sourceBuffer, targetFormat)
            {
                ResamplerQuality = 60 // highest quality
            };
        }

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();
        _running = true;
        _readThread.Start();
        CaptureStarted?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        _running = false;
        _capture?.StopRecording();
        _capture?.Dispose();
        _capture = null;
        _resampler?.Dispose();
        _resampler = null;
        CaptureStopped?.Invoke(this, EventArgs.Empty);
    }

    public byte[]? ReadFrame()
    {
        if (_resampler != null)
        {
            var frame = new byte[_frameSize];
            int read = _resampler.Read(frame, 0, _frameSize);
            if (read > 0)
            {
                if (read < _frameSize)
                    Array.Clear(frame, read, _frameSize - read);
                return frame;
            }
            return null;
        }

        if (_sourceBuffer == null) return null;
        if (_sourceBuffer.BufferedBytes >= _frameSize)
        {
            var frame = new byte[_frameSize];
            _sourceBuffer.Read(frame, 0, _frameSize);
            return frame;
        }
        return null;
    }

    public float[] GetAudioLevels(byte[] pcmData, int barCount)
    {
        var levels = new float[barCount];
        if (pcmData == null || pcmData.Length == 0) return levels;

        int bytesPerSample = _bitsPerSample / 8;
        int samplesPerBar = pcmData.Length / bytesPerSample / barCount;

        for (int i = 0; i < barCount; i++)
        {
            float sum = 0;
            int start = i * samplesPerBar * bytesPerSample;
            int end = Math.Min(start + samplesPerBar * bytesPerSample, pcmData.Length);

            for (int j = start; j < end; j += bytesPerSample)
            {
                float sample = 0f;
                if (bytesPerSample == 2)
                {
                    short val = (short)(pcmData[j] | (pcmData[j + 1] << 8));
                    sample = Math.Abs(val / 32768f);
                }
                else if (bytesPerSample == 3)
                {
                    int val = pcmData[j] | (pcmData[j + 1] << 8) | (pcmData[j + 2] << 16);
                    if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000);
                    sample = Math.Abs(val / 8388608f);
                }
                else if (bytesPerSample == 4)
                {
                    int val = BitConverter.ToInt32(pcmData, j);
                    sample = Math.Abs(val / 2147483648f);
                }
                sum += sample;
            }
            levels[i] = sum / Math.Max(samplesPerBar, 1);
        }
        return levels;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _sourceBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _running = false;
    }

    private void ReadLoop()
    {
        while (_running)
        {
            Thread.Sleep(1);
        }
    }

    public void Dispose()
    {
        Stop();
        _capture?.Dispose();
        _resampler?.Dispose();
    }
}

