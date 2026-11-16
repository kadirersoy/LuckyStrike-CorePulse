using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CorePulse.App.Controls;

public partial class MetricRow : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(MetricRow),
            new PropertyMetadata(string.Empty, (d, e) => ((MetricRow)d).TxtName.Text = (string)e.NewValue));

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(MetricRow),
            new PropertyMetadata(string.Empty, (d, e) => ((MetricRow)d).TxtValue.Text = (string)e.NewValue));

    public static readonly DependencyProperty HasBarProperty =
        DependencyProperty.Register(nameof(HasBar), typeof(bool), typeof(MetricRow),
            new PropertyMetadata(false, (d, e) => ((MetricRow)d).BarContainer.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed));

    public static readonly DependencyProperty BarPercentageProperty =
        DependencyProperty.Register(nameof(BarPercentage), typeof(double), typeof(MetricRow),
            new PropertyMetadata(0.0, (d, e) =>
            {
                var row = (MetricRow)d;
                double val = (double)e.NewValue;
                row.ProgressBar.Value = val;
                row.TxtPercent.Text = $"{val:F0}%";
            }));

    public static readonly DependencyProperty IsCriticalProperty =
        DependencyProperty.Register(nameof(IsCritical), typeof(bool), typeof(MetricRow),
            new PropertyMetadata(false, (d, e) => ((MetricRow)d).UpdateAlert((bool)e.NewValue)));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    public bool HasBar
    {
        get => (bool)GetValue(HasBarProperty);
        set => SetValue(HasBarProperty, value);
    }

    public double BarPercentage
    {
        get => (double)GetValue(BarPercentageProperty);
        set => SetValue(BarPercentageProperty, value);
    }

    public bool IsCritical
    {
        get => (bool)GetValue(IsCriticalProperty);
        set => SetValue(IsCriticalProperty, value);
    }

    public MetricRow()
    {
        InitializeComponent();
        MouseEnter += (s, e) => RootBorder.Background = (Brush)FindResource("CardHoverBackground");
        MouseLeave += (s, e) => RootBorder.Background = Brushes.Transparent;
    }

    public void SetValueColor(Brush brush)
    {
        TxtValue.Foreground = brush;
        ProgressBar.Foreground = brush;
        TxtPercent.Foreground = brush;
    }

    private void UpdateAlert(bool isCritical)
    {
        if (isCritical)
        {
            AlertDot.Visibility = Visibility.Visible;
            TxtValue.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
            ProgressBar.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        else
        {
            AlertDot.Visibility = Visibility.Collapsed;
        }
    }
}
