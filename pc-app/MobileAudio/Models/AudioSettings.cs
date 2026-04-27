namespace MobileAudio.Models;

/// <summary>
/// Immutable audio streaming configuration.
/// </summary>
public sealed record AudioSettings(
    int SampleRate = 48000,
    int Channels = 2,
    int BitsPerSample = 16,
    int FrameDurationMs = 3,
    int UdpPort = 5000,
    int DiscoveryPort = 5001
)
{
    /// <summary>
    /// Size of one audio frame in bytes.
    /// </summary>
    public int BufferSize => SampleRate * Channels * (BitsPerSample / 8) * FrameDurationMs / 1000;
}

