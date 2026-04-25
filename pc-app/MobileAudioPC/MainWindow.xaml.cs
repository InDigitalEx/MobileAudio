using System;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MobileAudioPC.Audio;
using MobileAudioPC.Models;

namespace MobileAudioPC;

public partial class MainWindow : Window
{
    private readonly AudioSettings _settings;
    private readonly AudioCapture _audioCapture;
    private readonly UdpStreamer _udpStreamer;
    private readonly DiscoveryService _discoveryService;
    private readonly DispatcherTimer _visualizerTimer;
    private readonly float[] _audioLevels;

    public MainWindow()
    {
        InitializeComponent();
        _settings = new AudioSettings();
        _audioCapture = new AudioCapture(_settings.SampleRate, _settings.Channels, _settings.BitsPerSample, _settings.FrameDurationMs);
        _udpStreamer = new UdpStreamer(_settings, _audioCapture);
        _discoveryService = new DiscoveryService(_settings.DiscoveryPort, _settings.UdpPort);
        _audioLevels = new float[48];

        _visualizerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _visualizerTimer.Tick += OnVisualizerTick;

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var localIp = GetLocalIpAddress();
        IpTextBox.Text = localIp;
        _discoveryService.Start();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _visualizerTimer.Stop();
        _udpStreamer.Stop();
        _discoveryService.Stop();
        _audioCapture.Dispose();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var targetIp = TargetIpTextBox.Text.Trim();
        if (string.IsNullOrEmpty(targetIp))
        {
            MessageBox.Show("Введите IP-адрес телефона", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _udpStreamer.Start(targetIp, _settings.UdpPort);
            _visualizerTimer.Start();
            UpdateStatus(true);
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _udpStreamer.Stop();
        _visualizerTimer.Stop();
        Visualizer.Clear();
        UpdateStatus(false);
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    private void CopyIpButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(IpTextBox.Text);
        MessageBox.Show("IP скопирован в буфер обмена", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnVisualizerTick(object? sender, EventArgs e)
    {
        var frame = _audioCapture.ReadFrame();
        if (frame != null)
        {
            var levels = _audioCapture.GetAudioLevels(frame, _audioLevels.Length);
            Visualizer.UpdateLevels(levels);
        }
    }

    private void UpdateStatus(bool isStreaming)
    {
        if (isStreaming)
        {
            StatusEllipse.Fill = new SolidColorBrush(Colors.LimeGreen);
            StatusText.Text = "Стриминг...";
        }
        else
        {
            StatusEllipse.Fill = new SolidColorBrush(Colors.Gray);
            StatusText.Text = "Остановлено";
        }
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }
        }
        catch { }
        return "127.0.0.1";
    }
}

