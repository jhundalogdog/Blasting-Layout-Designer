using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Blasting_Layout.Models;

public sealed class BlastConnector : INotifyPropertyChanged
{
    private bool _isSelected;

    public BlastConnector(
        BlastHole startHole,
        BlastHole endHole,
        string delayType = "",
        string color = "#F2C94C",
        string? chainId = null,
        string? connectorId = null)
    {
        StartHole = startHole;
        EndHole = endHole;
        DelayType = delayType;
        Color = color;
        ChainId = chainId ?? Guid.NewGuid().ToString("N");
        ConnectorId = connectorId ?? Guid.NewGuid().ToString("N");
        StartHole.PropertyChanged += OnEndpointChanged;
        EndHole.PropertyChanged += OnEndpointChanged;
    }

    public string ChainId { get; }

    public string ConnectorId { get; }

    public BlastHole StartHole { get; }

    public BlastHole EndHole { get; }

    public string DelayType { get; set; }

    public string Color { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public double Left => GetArrowPoints().Min(point => point.X);

    public double Top => GetArrowPoints().Min(point => point.Y);

    public Geometry ArrowGeometry
    {
        get
        {
            var points = GetArrowPoints();
            var left = points.Min(point => point.X);
            var top = points.Min(point => point.Y);
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(ToLocal(points[0], left, top), false, false);
                context.LineTo(ToLocal(points[1], left, top), true, false);
                context.BeginFigure(ToLocal(points[1], left, top), false, false);
                context.LineTo(ToLocal(points[2], left, top), true, false);
                context.BeginFigure(ToLocal(points[1], left, top), false, false);
                context.LineTo(ToLocal(points[3], left, top), true, false);
            }

            geometry.Freeze();
            return geometry;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Detach()
    {
        StartHole.PropertyChanged -= OnEndpointChanged;
        EndHole.PropertyChanged -= OnEndpointChanged;
    }

    private void OnEndpointChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(BlastHole.CenterX) or nameof(BlastHole.CenterY)))
        {
            return;
        }

        OnPropertyChanged(nameof(Left));
        OnPropertyChanged(nameof(Top));
        OnPropertyChanged(nameof(ArrowGeometry));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private Point[] GetArrowPoints()
    {
        var start = new Point(StartHole.CenterX, StartHole.CenterY);
        var end = new Point(EndHole.CenterX, EndHole.CenterY);
        var direction = end - start;
        direction.Normalize();

        var arrowBase = end - direction * 12;
        var perpendicular = new Vector(-direction.Y, direction.X) * 5;
        return [start, end, arrowBase + perpendicular, arrowBase - perpendicular];
    }

    private static Point ToLocal(Point point, double left, double top) =>
        new(point.X - left, point.Y - top);
}
