using Labeling.Domain.Consensus;

namespace Labeling.Application.Models;

public sealed record BoxDto(double X, double Y, double W, double H, double Score = 1.0)
{
    public static BoxDto From(BoundingBox b) => new(b.X, b.Y, b.W, b.H, b.Score);
    public BoundingBox ToDomain() => new(X, Y, W, H, Score);
}
