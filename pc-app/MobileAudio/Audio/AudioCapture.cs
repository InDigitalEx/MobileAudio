using System;
using System.Diagnostics;
using System.Threading;
using MobileAudio.Models;
using NAudio.Wave;

namespace MobileAudio.Audio;

/// <summary>
/// Captures system audio output via WASAPI loopback and raises
/// <see cref="FrameCaptured"/> events with fixed-size PCM frames.
/// </summary>
public sealed class AudioCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _sourceBuffer;
    private BufferedWaveProvider? _convertedBuffer;
    private FastResampler? _resampler;
    private Thread? _readThread;
    private readonly object _lock = new();
    private bool _running;

    private readonly int _frameSize;
    private readonly int _frameDurationMs;
    private readonly AudioSettings _settings;

    private readonly byte[] _convertBuffer = new byte[65536];

    public event EventHandler? CaptureStopped;
    public event EventHandler? CaptureStarted;
    public event EventHandler<byte[]>? FrameCaptured;

    public AudioCapture(AudioSettings settings)
    {
        _settings = settings;
        _frameDurationMs = settings.FrameDurationMs;
        _frameSize = settings.SampleRate * settings.Channels * (settings.BitsPerSample / 8) * _frameDurationMs / 1000;
        Debug.WriteLine($"[AudioCapture] FrameSize={_frameSize} bytes ({_frameDurationMs}ms)");
    }

    public void Start()
    {
        if (_running) return;

        // Ensure previous thread has terminated
        if (_readThread is { IsAlive: true })
        {
            _running = false;
            _readThread.Join(TimeSpan.FromSeconds(2));
            _readThread = null;
        }

        _capture = new WasapiLoopbackCapture();
        var fmt = _capture.WaveFormat;
        bool needsConversion = fmt.SampleRate != _settings.SampleRate ||
                               fmt.Channels != _settings.Channels ||
                               fmt.BitsPerSample != _settings.BitsPerSample ||
                               fmt.Encoding != WaveFormatEncoding.Pcm;

        Debug.WriteLine($"[AudioCapture] System format: {fmt} → Target: {_settings.SampleRate}Hz/{_settings.BitsPerSample}bit/{_settings.Channels}ch, needsConversion={needsConversion}");

        _sourceBuffer = new BufferedWaveProvider(fmt)
        {
            BufferLength = fmt.AverageBytesPerSecond / 10, // ~100ms
            DiscardOnBufferOverflow = true
        };

        if (needsConversion)
        {
            var targetFormat = new WaveFormat(_settings.SampleRate, _settings.BitsPerSample, _settings.Channels);
            _resampler = new FastResampler(fmt, targetFormat);

            _convertedBuffer = new BufferedWaveProvider(targetFormat)
            {
                BufferLength = targetFormat.AverageBytesPerSecond / 10, // ~100ms
                DiscardOnBufferOverflow = true
            };

            Debug.WriteLine($"[AudioCapture] FastResampler initialized");
        }

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();

        _running = true;
        _readThread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "AudioCapture.ReadLoop"
        };
        _readThread.Start();

        CaptureStarted?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine("[AudioCapture] Started");
    }

    public void Stop()
    {
        Debug.WriteLine("[AudioCapture] Stopping...");
        _running = false;
        _capture?.StopRecording();

        if (_readThread is { IsAlive: true })
        {
            _readThread.Join(TimeSpan.FromSeconds(3));
            if (_readThread.IsAlive)
            {
                Debug.WriteLine("[AudioCapture] WARNING: Read thread did not terminate gracefully");
            }
        }
        _readThread = null;

        lock (_lock)
        {
            _capture?.Dispose();
            _capture = null;
            _sourceBuffer = null;
            _convertedBuffer = null;
            _resampler = null;
        }

        CaptureStopped?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine("[AudioCapture] Stopped");
    }

    public byte[]? ReadFrame()
    {
        lock (_lock)
        {
            var provider = _convertedBuffer ?? _sourceBuffer;
            if (provider == null) return null;

            var frame = new byte[_frameSize];
            int read;
            try
            {
                read = provider.Read(frame, 0, _frameSize);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioCapture] Read error: {ex.Message}");
                return null;
            }

            if (read <= 0) return null;
            if (read < _frameSize)
                Array.Clear(frame, read, _frameSize - read);
            return frame;
        }
    }

    /// <summary>
    /// Computes per-bar audio levels from <paramref name="pcmData"/>.
    /// Reuses an internal buffer to avoid repeated allocations.
    /// </summary>
    public float[] GetAudioLevels(byte[] pcmData, int barCount)
    {
        if (barCount <= 0) throw new ArgumentOutOfRangeException(nameof(barCount));

        var levels = new float[barCount];
        if (pcmData == null || pcmData.Length == 0) return levels;

        int bytesPerSample = _settings.BitsPerSample / 8;
        int samplesPerBar = pcmData.Length / bytesPerSample / barCount;
        if (samplesPerBar <= 0) return levels;

        for (int i = 0; i < barCount; i++)
        {
            float sum = 0;
            int start = i * samplesPerBar * bytesPerSample;
            int end = Math.Min(start + samplesPerBar * bytesPerSample, pcmData.Length);

            for (int j = start; j < end; j += bytesPerSample)
            {
                float sample = bytesPerSample switch
                {
                    2 => Math.Abs((short)(pcmData[j] | (pcmData[j + 1] << 8)) / 32768f),
                    3 => Math.Abs(ReadInt24(pcmData, j) / 8388608f),
                    4 => Math.Abs(BitConverter.ToInt32(pcmData, j) / 2147483648f),
                    _ => 0f
                };
                sum += sample;
            }
            levels[i] = sum / samplesPerBar;
        }
        return levels;
    }

    private static int ReadInt24(byte[] data, int offset)
    {
        int val = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
        if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000);
        return val;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            if (_resampler != null && _convertedBuffer != null)
            {
                int outputBytes = _resampler.Process(e.Buffer, e.BytesRecorded, _convertBuffer);
                if (outputBytes > 0)
                {
                    _convertedBuffer.AddSamples(_convertBuffer, 0, outputBytes);
                }
            }
            else
            {
                _sourceBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _running = false;
        Debug.WriteLine("[AudioCapture] Recording stopped");
    }

    private void ReadLoop()
    {
        Debug.WriteLine("[AudioCapture] ReadLoop started");
        int frameCount = 0;
        var stopwatch = Stopwatch.StartNew();
        long nextFrameTicks = stopwatch.ElapsedTicks;
        long frameIntervalTicks = Stopwatch.Frequency * _frameDurationMs / 1000;

        while (_running)
        {
            try
            {
                var frame = ReadFrame();
                if (frame != null)
                {
                    frameCount++;
                    if (frameCount % 100 == 0)
                        Debug.WriteLine($"[AudioCapture] Frames captured: {frameCount}");

                    FrameCaptured?.Invoke(this, frame);

                    nextFrameTicks += frameIntervalTicks;
                    long sleepTicks = nextFrameTicks - stopwatch.ElapsedTicks;
                    if (sleepTicks > 0)
                    {
                        Thread.Sleep((int)(sleepTicks * 1000 / Stopwatch.Frequency));
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioCapture] ReadLoop error: {ex}");
                Thread.Sleep(100);
            }
        }
        Debug.WriteLine("[AudioCapture] ReadLoop ended");
    }

    public void Dispose()
    {
        Stop();
    }
}
