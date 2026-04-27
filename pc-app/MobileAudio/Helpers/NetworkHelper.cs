using System.Net;
using System.Net.Sockets;

namespace MobileAudio.Helpers;

/// <summary>
/// Network utility methods shared across the application.
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// Returns the first non-loopback IPv4 address of the local machine,
    /// or <c>127.0.0.1</c> as a fallback.
    /// </summary>
    public static string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkHelper] Failed to get local IP: {ex.Message}");
        }
        return "127.0.0.1";
    }
}

