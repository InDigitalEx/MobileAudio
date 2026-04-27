using System;
using System.Threading;
using NAudio.Wave;

namespace MobileAudio.Audio;

public class AudioCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _sourceBuffer;
    private IWaveProvider? _resampler;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _bitsPerSample;
    private readonly int _frameDurationMs;
    private readonly int _frameSize;
    private Thread? _readThread;
    private readonly object _lock = new();
    private bool _running;
    private bool _needsConversion;

    public event EventHandler? CaptureStopped;
    public event EventHandler? CaptureStarted;
    public event EventHandler<byte[]>? FrameCaptured;

    public AudioCapture(int sampleRate = 48000, int channels = 2, int bitsPerSample = 16, int frameDurationMs = 10)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        _bitsPerSample = bitsPerSample;
        _frameDurationMs = frameDurationMs;
        _frameSize = sampleRate * channels * (bitsPerSample / 8) * frameDurationMs / 1000;
        Console.WriteLine($"[AudioCapture] FrameSize={_frameSize} bytes ({frameDurationMs}ms), TargetFormat={sampleRate}Hz/{bitsPerSample}bit/{channels}ch");
    }

    public void Start()
    {
        if (_running) return;

        // Ensure previous thread has fully terminated
        if (_readThread != null && _readThread.IsAlive)
        {
            Console.WriteLine("[AudioCapture] Waiting for previous thread to finish...");
            _running = false;
            _readThread.Join(TimeSpan.FromSeconds(2));
            _readThread = null;
        }

        _capture = new WasapiLoopbackCapture();
        var fmt = _capture.WaveFormat;
        _needsConversion = fmt.SampleRate != _sampleRate || fmt.Channels != _channels || fmt.BitsPerSample != _bitsPerSample;

        Console.WriteLine($"[AudioCapture] System format: {fmt}");
        Console.WriteLine($"[AudioCapture] Needs conversion: {_needsConversion}");

        _sourceBuffer = new BufferedWaveProvider(fmt)
        {
            BufferLength = fmt.AverageBytesPerSecond * 2,
            DiscardOnBufferOverflow = true
        };

        if (_needsConversion)
        {
            try
            {
                _resampler = new WaveFloatTo16Provider(_sourceBuffer);
                Console.WriteLine("[AudioCapture] WaveFloatTo16Provider created");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioCapture] Failed to create converter: {ex.Message}");
                _resampler = null;
            }
        }

        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        _capture.StartRecording();

        _running = true;
        _readThread = new Thread(ReadLoop) { IsBackground = true, Priority = ThreadPriority.Highest };
        _readThread.Start();

        CaptureStarted?.Invoke(this, EventArgs.Empty);
        Console.WriteLine("[AudioCapture] Started");
    }

    public void Stop()
    {
        Console.WriteLine("[AudioCapture] Stopping...");
        _running = false;

        _capture?.StopRecording();

        // Wait for read thread to finish
        if (_readThread != null && _readThread.IsAlive)
        {
            _readThread.Join(TimeSpan.FromSeconds(3));
            if (_readThread.IsAlive)
            {
                Console.WriteLine("[AudioCapture] WARNING: Read thread did not terminate gracefully");
            }
        }
        _readThread = null;

        lock (_lock)
        {
            _capture?.Dispose();
            _capture = null;
            (_resampler as IDisposable)?.Dispose();
            _resampler = null;
            _sourceBuffer = null;
        }

        CaptureStopped?.Invoke(this, EventArgs.Empty);
        Console.WriteLine("[AudioCapture] Stopped");
    }

    public byte[]? ReadFrame()
    {
        lock (_lock)
        {
            if (_resampler != null)
            {
                var frame = new byte[_frameSize];
                int read = 0;
                try
                {
                    read = _resampler.Read(frame, 0, _frameSize);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AudioCapture] Resampler read error: {ex.Message}");
                    return null;
                }

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
        lock (_lock)
        {
            _sourceBuffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _running = false;
        Console.WriteLine("[AudioCapture] Recording stopped");
    }

    private void ReadLoop()
    {
        Console.WriteLine("[AudioCapture] ReadLoop started");
        int frameCount = 0;
        var nextFrameTime = DateTime.UtcNow;
        while (_running)
        {
            try
            {
                var frame = ReadFrame();
                if (frame != null)
                {
                    frameCount++;
                    if (frameCount % 100 == 0)
                        Console.WriteLine($"[AudioCapture] Frames captured: {frameCount}");
                    FrameCaptured?.Invoke(this, frame);
                    nextFrameTime = nextFrameTime.AddMilliseconds(_frameDurationMs);
                    var sleepMs = (int)(nextFrameTime - DateTime.UtcNow).TotalMilliseconds;
                    if (sleepMs > 0)
                        Thread.Sleep(sleepMs);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioCapture] ReadLoop error: {ex}");
                Thread.Sleep(100);
            }
        }
        Console.WriteLine("[AudioCapture] ReadLoop ended");
    }

    public void Dispose()
    {
        Stop();
    }
}
