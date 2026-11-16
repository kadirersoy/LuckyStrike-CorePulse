using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CorePulse.App.Controls;

public partial class GlowBarMeter : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(GlowBarMeter),
            new PropertyMetadata(string.Empty, (d, e) => ((GlowBarMeter)d).TxtTitle.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(GlowBarMeter),
            new PropertyMetadata(0.0, (d, e) => ((GlowBarMeter)d).UpdateValue((double)e.NewValue)));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(GlowBarMeter),
            new PropertyMetadata(100.0, (d, e) =>
            {
                var meter = (GlowBarMeter)d;
                meter.BarValue.Maximum = (double)e.NewValue;
                meter.TxtMaxScale.Text = ((double)e.NewValue).ToString("F0");
            }));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(GlowBarMeter),
            new PropertyMetadata(string.Empty, (d, e) => ((GlowBarMeter)d).TxtUnit.Text = (string)e.NewValue));

    public static readonly DependencyProperty IsCriticalProperty =
        DependencyProperty.Register(nameof(IsCritical), typeof(bool), typeof(GlowBarMeter),
            new PropertyMetadata(false, (d, e) => ((GlowBarMeter)d).UpdateVisualState()));

    public static readonly DependencyProperty IsWarningProperty =
        DependencyProperty.Register(nameof(IsWarning), typeof(bool), typeof(GlowBarMeter),
            new PropertyMetadata(false, (d, e) => ((GlowBarMeter)d).UpdateVisualState()));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public bool IsCritical
    {
        get => (bool)GetValue(IsCriticalProperty);
        set => SetValue(IsCriticalProperty, value);
    }

    public bool IsWarning
    {
        get => (bool)GetValue(IsWarningProperty);
        set => SetValue(IsWarningProperty, value);
    }

    public GlowBarMeter()
    {
        InitializeComponent();
    }

    public void SetThresholdInfo(string thresholdText)
    {
        TxtThreshold.Text = thresholdText;
    }

    public void SetBarBrush(Brush brush)
    {
        BarValue.Foreground = brush;
    }

    private void UpdateValue(double val)
    {
        TxtValue.Text = val.ToString("F0");
        BarValue.Value = val;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (IsCritical)
        {
            RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            BadgeBorder.Background = new SolidColorBrush(Color.FromRgb(69, 10, 10));
            TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            TxtStatus.Text = "KRİTİK EŞİK!";
            BarValue.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        else if (IsWarning)
        {
            RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            BadgeBorder.Background = new SolidColorBrush(Color.FromRgb(69, 26, 3));
            TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36));
            TxtStatus.Text = "YÜKSEK";
            BarValue.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
        else
        {
            RootBorder.BorderBrush = (Brush)FindResource("CardBorder");
            BadgeBorder.Background = new SolidColorBrush(Color.FromRgb(5, 46, 22));
            TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            TxtStatus.Text = "NORMAL";
        }
    }
}
