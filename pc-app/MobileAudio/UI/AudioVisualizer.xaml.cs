using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MobileAudio.UI;

public partial class AudioVisualizer : UserControl
{
    private readonly Rectangle[] _bars;
    private const int BarCount = 48;

    public AudioVisualizer()
    {
        InitializeComponent();
        _bars = new Rectangle[BarCount];
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CreateBars();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        CreateBars();
    }

    private void CreateBars()
    {
        VisualizerCanvas.Children.Clear();
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (width == 0 || height == 0) return;

        var barWidth = (width - (BarCount - 1) * 2) / BarCount;
        for (int i = 0; i < BarCount; i++)
        {
            var rect = new Rectangle
            {
                Width = barWidth,
                Height = 2,
                Fill = new SolidColorBrush(Color.FromRgb(0, 200, 180)),
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(rect, i * (barWidth + 2));
            Canvas.SetBottom(rect, 0);
            _bars[i] = rect;
            VisualizerCanvas.Children.Add(rect);
        }
    }

    public void UpdateLevels(float[] levels)
    {
        if (levels == null || levels.Length == 0) return;
        if (_bars[0] == null) CreateBars();

        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (height == 0) return;

        var step = levels.Length / BarCount;
        for (int i = 0; i < BarCount; i++)
        {
            if (_bars[i] == null) continue;
            var idx = Math.Min(i * step, levels.Length - 1);
            var level = Math.Min(levels[idx], 1.0f);
            _bars[i].Height = Math.Max(2, level * height);

            var brush = _bars[i].Fill as SolidColorBrush;
            if (brush != null)
            {
                byte r = (byte)(level > 0.6 ? 255 : 0);
                byte g = (byte)(200 - level * 100);
                byte b = (byte)(180 - level * 50);
                brush.Color = Color.FromRgb(r, g, b);
            }
        }
    }

    public void Clear()
    {
        if (_bars[0] == null) return;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        foreach (var bar in _bars)
        {
            if (bar == null) continue;
            bar.Height = 2;
            if (bar.Fill is SolidColorBrush brush)
                brush.Color = Color.FromRgb(0, 200, 180);
        }
    }
}

