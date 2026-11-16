using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CorePulse.App.Controls;

public partial class CircularRingGauge : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(CircularRingGauge),
            new PropertyMetadata(string.Empty, (d, e) => ((CircularRingGauge)d).TxtTitle.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(CircularRingGauge),
            new PropertyMetadata(0.0, (d, e) => ((CircularRingGauge)d).UpdateProgress((double)e.NewValue)));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(CircularRingGauge),
            new PropertyMetadata(100.0, (d, e) => ((CircularRingGauge)d).UpdateProgress(((CircularRingGauge)d).Value)));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(CircularRingGauge),
            new PropertyMetadata(string.Empty, (d, e) => ((CircularRingGauge)d).TxtUnit.Text = (string)e.NewValue));

    public static readonly DependencyProperty ProgressBrushProperty =
        DependencyProperty.Register(nameof(ProgressBrush), typeof(Brush), typeof(CircularRingGauge),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is Brush b) ((CircularRingGauge)d).ArcProgress.Stroke = b;
            }));

    public static readonly DependencyProperty IsCriticalProperty =
        DependencyProperty.Register(nameof(IsCritical), typeof(bool), typeof(CircularRingGauge),
            new PropertyMetadata(false, (d, e) => ((CircularRingGauge)d).UpdateAlertState()));

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

    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public bool IsCritical
    {
        get => (bool)GetValue(IsCriticalProperty);
        set => SetValue(IsCriticalProperty, value);
    }

    public CircularRingGauge()
    {
        InitializeComponent();
        UpdateProgress(0.0);
    }

    public void SetDetails(string min, string max, string sub)
    {
        TxtMin.Text = min;
        TxtMax.Text = max;
        TxtSub.Text = sub;
    }

    private void UpdateProgress(double val)
    {
        TxtValue.Text = val.ToString("F0");

        double max = Maximum <= 0 ? 100 : Maximum;
        double ratio = Math.Clamp(val / max, 0.001, 0.9999);
        double angle = ratio * 360.0;

        // Center = (85, 85), Radius = 70, Start at Top (85, 15)
        double rad = (angle - 90.0) * Math.PI / 180.0;
        double x = 85.0 + 70.0 * Math.Cos(rad);
        double y = 85.0 + 70.0 * Math.Sin(rad);

        ArcSegmentProgress.Point = new Point(x, y);
        ArcSegmentProgress.IsLargeArc = angle > 180.0;
    }

    private void UpdateAlertState()
    {
        if (IsCritical)
        {
            RootBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            TxtValue.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
        }
        else
        {
            RootBorder.BorderBrush = (Brush)FindResource("CardBorder");
            TxtValue.Foreground = (Brush)FindResource("TextPrimary");
        }
    }
}
