namespace MobileAudio.Models;

public class AudioSettings
{
    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 2;
    public int BitsPerSample { get; set; } = 16;
    public int FrameDurationMs { get; set; } = 5;
    public int UdpPort { get; set; } = 5000;
    public int DiscoveryPort { get; set; } = 5001;
    public int BufferSize => SampleRate * Channels * (BitsPerSample / 8) * FrameDurationMs / 1000;
}

