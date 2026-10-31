using System.Windows;
using Blasting_Layout.Models;

namespace Blasting_Layout.Services;

public sealed class BlastConnectionPathService
{
    private const double PositionTolerance = 1.5;
    private const double NeighborDistanceTolerance = 1.5;

    private static readonly double MaximumNeighborDistance =
        Math.Sqrt(Math.Pow(BlastLayoutService.HoleSpacing, 2) + Math.Pow(BlastLayoutService.RowSpacing, 2)) +
        NeighborDistanceTolerance;

    public bool TryFindStraightPath(
        IReadOnlyCollection<BlastHole> holes,
        BlastHole startHole,
        BlastHole endHole,
        out IReadOnlyList<BlastHole> path)
    {
        path = Array.Empty<BlastHole>();
        if (startHole == endHole || !holes.Contains(startHole) || !holes.Contains(endHole))
        {
            return false;
        }

        var start = new Point(startHole.CenterX, startHole.CenterY);
        var end = new Point(endHole.CenterX, endHole.CenterY);
        var total = end - start;
        var candidatePaths = new List<IReadOnlyList<BlastHole>>();

        foreach (var neighbor in holes.Where(hole => hole != startHole))
        {
            var neighborPoint = new Point(neighbor.CenterX, neighbor.CenterY);
            var step = neighborPoint - start;
            var stepLength = step.Length;
            if (stepLength <= PositionTolerance || stepLength > MaximumNeighborDistance)
            {
                continue;
            }

            var projectedSteps = Vector.Multiply(total, step) / step.LengthSquared;
            var stepCount = (int)Math.Round(projectedSteps);
            if (stepCount < 1 ||
                Math.Abs(projectedSteps - stepCount) * stepLength > PositionTolerance ||
                Math.Abs(Vector.CrossProduct(total, step)) / stepLength > PositionTolerance)
            {
                continue;
            }

            var candidatePath = new List<BlastHole> { startHole };
            var usedHoles = new HashSet<BlastHole> { startHole };
            var isComplete = true;

            for (var index = 1; index <= stepCount; index++)
            {
                var expected = start + step * index;
                var matches = holes
                    .Where(hole => !usedHoles.Contains(hole))
                    .Where(hole => Distance(
                        new Point(hole.CenterX, hole.CenterY),
                        expected) <= PositionTolerance)
                    .ToArray();

                if (matches.Length != 1)
                {
                    isComplete = false;
                    break;
                }

                var nextHole = matches[0];
                var previousHole = candidatePath[^1];
                if (Distance(
                        new Point(previousHole.CenterX, previousHole.CenterY),
                        new Point(nextHole.CenterX, nextHole.CenterY)) > MaximumNeighborDistance)
                {
                    isComplete = false;
                    break;
                }

                candidatePath.Add(nextHole);
                usedHoles.Add(nextHole);
            }

            if (isComplete && candidatePath[^1] == endHole)
            {
                candidatePaths.Add(candidatePath);
            }
        }

        var uniquePaths = candidatePaths
            .GroupBy(candidate => string.Join("|", candidate.Select(hole => hole.HoleId)))
            .Select(group => group.First())
            .OrderBy(candidate => candidate.Count)
            .ToArray();

        if (uniquePaths.Length == 0)
        {
            return false;
        }

        path = uniquePaths[0];
        return true;
    }

    private static double Distance(Point first, Point second) =>
        (first - second).Length;
}
