using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MobileAudio.UI;

/// <summary>
/// A bar-style audio visualizer with 48 vertical bars.
/// </summary>
public partial class AudioVisualizer : UserControl
{
    private readonly Rectangle[] _bars;
    private const int BarCount = 48;
    private const int BarSpacing = 2;
    private const double MinBarHeight = 2.0;

    public AudioVisualizer()
    {
        InitializeComponent();
        _bars = new Rectangle[BarCount];
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => CreateBars();
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => CreateBars();

    private void CreateBars()
    {
        VisualizerCanvas.Children.Clear();
        Array.Clear(_bars, 0, _bars.Length);

        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (width <= 0 || height <= 0) return;

        double barWidth = Math.Max(1.0, (width - (BarCount - 1) * BarSpacing) / BarCount);

        for (int i = 0; i < BarCount; i++)
        {
            var rect = new Rectangle
            {
                Width = barWidth,
                Height = MinBarHeight,
                Fill = new SolidColorBrush(Color.FromRgb(0, 200, 180)),
                RadiusX = 2,
                RadiusY = 2
            };
            Canvas.SetLeft(rect, i * (barWidth + BarSpacing));
            Canvas.SetBottom(rect, 0);
            _bars[i] = rect;
            VisualizerCanvas.Children.Add(rect);
        }
    }

    /// <summary>
    /// Updates bar heights and colors from the provided audio levels.
    /// </summary>
    public void UpdateLevels(float[] levels)
    {
        if (levels == null || levels.Length == 0) return;
        if (!HasBars) CreateBars();

        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (height <= 0) return;

        int step = Math.Max(1, levels.Length / BarCount);

        for (int i = 0; i < BarCount; i++)
        {
            var bar = _bars[i];
            if (bar == null) continue;

            int idx = Math.Min(i * step, levels.Length - 1);
            float level = Math.Clamp(levels[idx], 0.0f, 1.0f);
            bar.Height = Math.Max(MinBarHeight, level * height);

            if (bar.Fill is SolidColorBrush brush)
            {
                byte r = (byte)(level > 0.6 ? 255 : 0);
                byte g = (byte)(200 - level * 100);
                byte b = (byte)(180 - level * 50);
                brush.Color = Color.FromRgb(r, g, b);
            }
        }
    }

    /// <summary>
    /// Resets all bars to their default state.
    /// </summary>
    public void Clear()
    {
        if (!HasBars) return;

        foreach (var bar in _bars)
        {
            if (bar == null) continue;
            bar.Height = MinBarHeight;
            if (bar.Fill is SolidColorBrush brush)
                brush.Color = Color.FromRgb(0, 200, 180);
        }
    }

    private bool HasBars => _bars.Length > 0 && _bars[0] != null;
}

