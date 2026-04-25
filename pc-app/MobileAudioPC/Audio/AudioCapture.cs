using System;
using System.Threading;
using NAudio.Wave;

namespace MobileAudioPC.Audio;

public class AudioCapture : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private BufferedWaveProvider? _sourceBuffer;
    private readonly int _sampleRate;
    private readonly int _channels;
    private readonly int _bitsPerSample;
    private readonly int _frameSize;
    private readonly Thread _readThread;
    private bool _running;
    private int _sourceBytesPerSample;
    private int _sourceChannels;
    private int _sourceSampleRate;
    private bool _needsConversion;

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
        var fmt = _capture.WaveFormat;
        _sourceBytesPerSample = fmt.BitsPerSample / 8;
        _sourceChannels = fmt.Channels;
        _sourceSampleRate = fmt.SampleRate;
        _needsConversion = fmt.SampleRate != _sampleRate || fmt.Channels != _channels || fmt.BitsPerSample != _bitsPerSample;

        _sourceBuffer = new BufferedWaveProvider(fmt)
        {
            BufferLength = fmt.AverageBytesPerSecond * 2,
            DiscardOnBufferOverflow = true
        };

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
        CaptureStopped?.Invoke(this, EventArgs.Empty);
    }

    public byte[]? ReadFrame()
    {
        if (_sourceBuffer == null) return null;

        if (_needsConversion)
        {
            int sourceFrameSize = _frameSize * _sourceBytesPerSample * _sourceChannels / ((_bitsPerSample / 8) * _channels);
            sourceFrameSize = Math.Max(sourceFrameSize, _sourceChannels * _sourceBytesPerSample);

            if (_sourceBuffer.BufferedBytes < sourceFrameSize) return null;

            var sourceFrame = new byte[sourceFrameSize];
            _sourceBuffer.Read(sourceFrame, 0, sourceFrameSize);
            return ConvertToTargetFormat(sourceFrame);
        }

        if (_sourceBuffer.BufferedBytes >= _frameSize)
        {
            var frame = new byte[_frameSize];
            _sourceBuffer.Read(frame, 0, _frameSize);
            return frame;
        }
        return null;
    }

    private byte[] ConvertToTargetFormat(byte[] source)
    {
        int targetBytesPerSample = _bitsPerSample / 8;
        int sourceSamples = source.Length / (_sourceBytesPerSample * _sourceChannels);
        int targetSamples = _sampleRate * sourceSamples / _sourceSampleRate;
        targetSamples = Math.Max(targetSamples, 1);

        var result = new byte[targetSamples * _channels * targetBytesPerSample];

        for (int i = 0; i < targetSamples; i++)
        {
            float t = (float)i / targetSamples;
            int srcIdx = (int)(t * sourceSamples);
            srcIdx = Math.Min(srcIdx, sourceSamples - 1);

            for (int ch = 0; ch < _channels; ch++)
            {
                int srcCh = Math.Min(ch, _sourceChannels - 1);
                int srcOffset = (srcIdx * _sourceChannels + srcCh) * _sourceBytesPerSample;
                float sample = 0f;

                if (srcOffset + _sourceBytesPerSample <= source.Length)
                {
                    if (_sourceBytesPerSample == 1)
                    {
                        sample = (source[srcOffset] - 128) / 128f;
                    }
                    else if (_sourceBytesPerSample == 2)
                    {
                        short val = (short)(source[srcOffset] | (source[srcOffset + 1] << 8));
                        sample = val / 32768f;
                    }
                    else if (_sourceBytesPerSample == 3)
                    {
                        int val = source[srcOffset] | (source[srcOffset + 1] << 8) | (source[srcOffset + 2] << 16);
                        if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000);
                        sample = val / 8388608f;
                    }
                    else if (_sourceBytesPerSample == 4)
                    {
                        if (_capture?.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                        {
                            sample = BitConverter.ToSingle(source, srcOffset);
                        }
                        else
                        {
                            int val = BitConverter.ToInt32(source, srcOffset);
                            sample = val / 2147483648f;
                        }
                    }
                }

                sample = Math.Max(-1f, Math.Min(1f, sample));

                int dstOffset = (i * _channels + ch) * targetBytesPerSample;
                if (targetBytesPerSample == 2)
                {
                    short val16 = (short)(sample * 32767);
                    result[dstOffset] = (byte)(val16 & 0xFF);
                    result[dstOffset + 1] = (byte)((val16 >> 8) & 0xFF);
                }
                else if (targetBytesPerSample == 3)
                {
                    int val24 = (int)(sample * 8388607);
                    result[dstOffset] = (byte)(val24 & 0xFF);
                    result[dstOffset + 1] = (byte)((val24 >> 8) & 0xFF);
                    result[dstOffset + 2] = (byte)((val24 >> 16) & 0xFF);
                }
                else if (targetBytesPerSample == 4)
                {
                    int val32 = (int)(sample * 2147483647);
                    BitConverter.GetBytes(val32).CopyTo(result, dstOffset);
                }
            }
        }

        return result;
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
    }
}

