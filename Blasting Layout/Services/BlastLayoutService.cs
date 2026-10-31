using Blasting_Layout.Models;

namespace Blasting_Layout.Services;

public enum BlastPattern
{
    Square,
    Staggered
}

public enum StaggerDirection
{
    Left,
    Right,
    AlternateRight,
    AlternateLeft
}

public enum RowInsertionDirection
{
    Below,
    Above
}

public sealed class BlastLayoutService
{
    public const double GridSpacing = 25;
    public const double InitialCanvasExtent = 20000;
    public const double InitialPlotCenter = InitialCanvasExtent / 2;

    public const double HoleSpacing = 100;
    public const double RowSpacing = 100;

    public (double X, double Y) GetHolePosition(
        int rowIndex,
        int holeIndex,
        BlastPattern pattern,
        StaggerDirection direction)
    {
        var staggerOffset = GetStaggerOffset(rowIndex, pattern, direction);

        return (
            InitialPlotCenter + staggerOffset + holeIndex * HoleSpacing - BlastHole.MarkerDiameter / 2,
            InitialPlotCenter + rowIndex * RowSpacing - BlastHole.MarkerDiameter / 2);
    }

    public double GetStaggerOffset(int rowIndex, BlastPattern pattern, StaggerDirection direction)
    {
        if (rowIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex), rowIndex, "Row index cannot be negative.");
        }

        if (pattern == BlastPattern.Square)
        {
            return 0;
        }

        return direction switch
        {
            StaggerDirection.Right => rowIndex * HoleSpacing / 2,
            StaggerDirection.Left => -rowIndex * HoleSpacing / 2,
            StaggerDirection.AlternateRight => rowIndex % 2 == 0 ? 0 : HoleSpacing / 2,
            StaggerDirection.AlternateLeft => rowIndex % 2 == 0 ? 0 : -(HoleSpacing / 2),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported stagger direction.")
        };
    }

    public double SnapHolePositionToGrid(double position)
    {
        var markerRadius = BlastHole.MarkerDiameter / 2;
        var snappedCenter = Math.Round((position + markerRadius) / GridSpacing) * GridSpacing;
        return Math.Max(0, Math.Max(GridSpacing, snappedCenter) - markerRadius);
    }
}
