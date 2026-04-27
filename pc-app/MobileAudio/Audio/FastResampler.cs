using System;
using NAudio.Wave;

namespace MobileAudio.Audio;

/// <summary>
/// Ultra-fast audio resampler with bit-depth conversion.
/// Uses integer decimation (no filtering overhead) for maximum speed.
/// Supports common ratios like 4:1 (192kHz → 48kHz).
/// </summary>
public sealed class FastResampler
{
    private readonly int _sourceBytesPerFrame;
    private readonly int _channels;
    private readonly int _decimationFactor;
    private readonly bool _sourceIsFloat;
    private readonly int _bytesPerSrcChannel;
    private int _phase;

    public FastResampler(WaveFormat source, WaveFormat target)
    {
        if (source.Channels != target.Channels)
            throw new ArgumentException("Channel conversion not supported");

        _channels = source.Channels;
        _sourceBytesPerFrame = source.Channels * (source.BitsPerSample / 8);
        _decimationFactor = source.SampleRate / target.SampleRate;
        if (_decimationFactor < 1) _decimationFactor = 1;
        _sourceIsFloat = source.Encoding == WaveFormatEncoding.IeeeFloat;
        _bytesPerSrcChannel = _sourceBytesPerFrame / _channels;
        _phase = 0;
    }

    /// <summary>
    /// Converts and decimates a block of input audio to 16-bit PCM.
    /// Returns number of bytes written to output.
    /// </summary>
    public int Process(byte[] input, int inputBytes, byte[] output)
    {
        int inputSampleCount = inputBytes / _sourceBytesPerFrame;
        int outPos = 0;

        for (int i = 0; i < inputSampleCount; i++)
        {
            if (_phase != 0)
            {
                _phase--;
                continue;
            }

            int srcOffset = i * _sourceBytesPerFrame;

            for (int ch = 0; ch < _channels; ch++)
            {
                short sample16 = _bytesPerSrcChannel switch
                {
                    4 when _sourceIsFloat => FloatToInt16(input, srcOffset + ch * 4),
                    4 => Int32ToInt16(input, srcOffset + ch * 4),
                    3 => Int24ToInt16(input, srcOffset + ch * 3),
                    2 => Int16ToInt16(input, srcOffset + ch * 2),
                    _ => 0
                };

                output[outPos++] = (byte)(sample16 & 0xFF);
                output[outPos++] = (byte)((sample16 >> 8) & 0xFF);
            }

            _phase = _decimationFactor - 1;
        }

        return outPos;
    }

    private static short FloatToInt16(byte[] input, int offset)
    {
        float f = BitConverter.ToSingle(input, offset);
        return (short)(f * 32767f);
    }

    private static short Int32ToInt16(byte[] input, int offset)
    {
        int val = BitConverter.ToInt32(input, offset);
        return (short)(val >> 16);
    }

    private static short Int24ToInt16(byte[] input, int offset)
    {
        int val = input[offset] | (input[offset + 1] << 8) | (input[offset + 2] << 16);
        if ((val & 0x800000) != 0) val |= unchecked((int)0xFF000000);
        return (short)(val >> 8);
    }

    private static short Int16ToInt16(byte[] input, int offset)
    {
        return (short)(input[offset] | (input[offset + 1] << 8));
    }
}
