using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace CorePulse.App.Controls;

public partial class SparklineGraph : UserControl
{
    private readonly Queue<double> _primaryValues = new(60);
    private readonly Queue<double> _secondaryValues = new(60);

    public SparklineGraph()
    {
        InitializeComponent();
        SizeChanged += (s, e) => Redraw();
    }

    public void AddPoints(double primaryVal, double secondaryVal)
    {
        if (_primaryValues.Count >= 60) _primaryValues.Dequeue();
        _primaryValues.Enqueue(primaryVal);

        if (_secondaryValues.Count >= 60) _secondaryValues.Dequeue();
        _secondaryValues.Enqueue(secondaryVal);

        Redraw();
    }

    private void Redraw()
    {
        double width = WaveCanvas.ActualWidth > 0 ? WaveCanvas.ActualWidth : 400.0;
        double height = WaveCanvas.ActualHeight > 0 ? WaveCanvas.ActualHeight : 70.0;

        // Draw primary line & area
        if (_primaryValues.Count > 1)
        {
            var points = new PointCollection();
            var areaPoints = new PointCollection();

            double stepX = width / Math.Max(1, _primaryValues.Count - 1);
            int idx = 0;

            foreach (var v in _primaryValues)
            {
                double norm = Math.Clamp(v / 100.0, 0.0, 1.0);
                double y = height - (norm * (height - 10.0)) - 5.0;
                double x = idx * stepX;
                points.Add(new Point(x, y));
                areaPoints.Add(new Point(x, y));
                idx++;
            }

            PolyPrimary.Points = points;

            // Close polygon for area
            areaPoints.Add(new Point(width, height));
            areaPoints.Add(new Point(0, height));
            PolyAreaPrimary.Points = areaPoints;
        }

        // Draw secondary line
        if (_secondaryValues.Count > 1)
        {
            var secPoints = new PointCollection();
            double stepX = width / Math.Max(1, _secondaryValues.Count - 1);
            int idx = 0;

            foreach (var v in _secondaryValues)
            {
                double norm = Math.Clamp(v / 100.0, 0.0, 1.0);
                double y = height - (norm * (height - 10.0)) - 5.0;
                double x = idx * stepX;
                secPoints.Add(new Point(x, y));
                idx++;
            }

            PolySecondary.Points = secPoints;
        }
    }
}
