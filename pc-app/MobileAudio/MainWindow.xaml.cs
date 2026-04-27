using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MobileAudio.Audio;
using MobileAudio.Models;

namespace MobileAudio;

public partial class MainWindow : Window
{
    private readonly AudioSettings _settings;
    private readonly AudioCapture _audioCapture;
    private readonly UdpStreamer _udpStreamer;
    private readonly DiscoveryService _discoveryService;
    private readonly DispatcherTimer _visualizerTimer;
    private byte[]? _lastFrame;
    private readonly object _frameLock = new();
    private int _visualizerFrames;

    public MainWindow()
    {
        InitializeComponent();
        _settings = new AudioSettings();
        _audioCapture = new AudioCapture(_settings.SampleRate, _settings.Channels, _settings.BitsPerSample, _settings.FrameDurationMs);
        _udpStreamer = new UdpStreamer(_settings);
        _discoveryService = new DiscoveryService(_settings.DiscoveryPort, _settings.UdpPort);

        _audioCapture.FrameCaptured += OnFrameCaptured;
        _audioCapture.CaptureStarted += (s, e) => Log("[MainWindow] Capture started");
        _audioCapture.CaptureStopped += (s, e) => Log("[MainWindow] Capture stopped");
        _udpStreamer.Started += (s, e) => Log("[MainWindow] Streamer started");
        _udpStreamer.Stopped += (s, e) => Log("[MainWindow] Streamer stopped");

        _visualizerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _visualizerTimer.Tick += OnVisualizerTick;

        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private void Log(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            LogScrollViewer.ScrollToEnd();
        });
    }

    private int _framesCaptured;
    private DateTime _lastStatsTime = DateTime.Now;

    private void OnFrameCaptured(object? sender, byte[] frame)
    {
        lock (_frameLock)
        {
            _lastFrame = frame;
        }
        _udpStreamer.SendFrame(frame);
        _framesCaptured++;

        // Log stats every 5 seconds
        if ((DateTime.Now - _lastStatsTime).TotalSeconds >= 5)
        {
            _lastStatsTime = DateTime.Now;
            Log($"[Stats] Frames captured: {_framesCaptured}, Streamer active: {_udpStreamer.IsStreaming}");
        }
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        var localIp = GetLocalIpAddress();
        IpTextBox.Text = localIp;
        _discoveryService.DevicesUpdated += OnDevicesUpdated;
        _discoveryService.Start();
        Log($"[MainWindow] Local IP: {localIp}");
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _visualizerTimer.Stop();
        _udpStreamer.Stop();
        _audioCapture.Stop();
        _discoveryService.Stop();
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
            Log("[MainWindow] Starting capture and stream...");
            _audioCapture.Start();
            _udpStreamer.Start(targetIp, _settings.UdpPort);
            _visualizerTimer.Start();
            UpdateStatus(true);
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            Log("[MainWindow] Started successfully");
        }
        catch (Exception ex)
        {
            Log($"[MainWindow] Start error: {ex}");
            MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        Log("[MainWindow] Stopping...");
        _udpStreamer.Stop();
        _audioCapture.Stop();
        _visualizerTimer.Stop();
        Visualizer.Clear();
        UpdateStatus(false);
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        _visualizerFrames = 0;
    }

    private void CopyIpButton_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(IpTextBox.Text);
        MessageBox.Show("IP скопирован в буфер обмена", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnDevicesUpdated(object? sender, List<DiscoveredDevice> devices)
    {
        Dispatcher.BeginInvoke(() =>
        {
            DevicesPanel.Children.Clear();
            if (devices.Count == 0)
            {
                DevicesPanel.Children.Add(new TextBlock
                {
                    Text = "Поиск устройств...",
                    FontSize = 13,
                    Opacity = 0.4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 8)
                });
                return;
            }

            foreach (var device in devices)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var nameTb = new TextBlock
                {
                    Text = $"{device.DeviceName}",
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Colors.White),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(nameTb, 0);

                var ipTb = new TextBlock
                {
                    Text = device.IpAddress,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(128, 203, 196)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                Grid.SetColumn(ipTb, 1);

                grid.Children.Add(nameTb);
                grid.Children.Add(ipTb);
                border.Child = grid;

                border.MouseLeftButtonDown += (s, ev) =>
                {
                    TargetIpTextBox.Text = device.IpAddress;
                    Log($"[Discovery] Selected device: {device.DeviceName} ({device.IpAddress})");
                };

                DevicesPanel.Children.Add(border);
            }
        });
    }

    private void OnVisualizerTick(object? sender, EventArgs e)
    {
        byte[]? frame;
        lock (_frameLock)
        {
            frame = _lastFrame;
        }

        if (frame != null)
        {
            try
            {
                var levels = _audioCapture.GetAudioLevels(frame, 48);
                Visualizer.UpdateLevels(levels);
                _visualizerFrames++;
                if (_visualizerFrames % 20 == 0)
                    Log($"[MainWindow] Visualizer updated, frames shown: {_visualizerFrames}");
            }
            catch (Exception ex)
            {
                Log($"[MainWindow] Visualizer error: {ex.Message}");
            }
        }
        else
        {
            Log("[MainWindow] No frame for visualizer");
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
