using Quorum.Domain;

namespace Labeling.Domain.Consensus;

/// <summary>
/// Value object: прямоугольник в нормированных координатах [0, 1] (левый верхний угол, ширина, высота)
/// и уверенность модели. Неизменяемый; хранится в JSON-колонке.
/// </summary>
public sealed class BoundingBox
{
    private BoundingBox() { }

    public BoundingBox(double x, double y, double w, double h, double score = 1.0)
    {
        if (w <= 0 || h <= 0) throw new DomainException("Box width and height must be positive");
        if (x < 0 || y < 0 || x + w > 1.0001 || y + h > 1.0001)
            throw new DomainException("Box must lie inside the frame (normalized coordinates)");
        if (score is < 0 or > 1) throw new DomainException("Score must be in [0, 1]");
        X = x; Y = y; W = w; H = h; Score = score;
    }

    public double X { get; private set; }
    public double Y { get; private set; }
    public double W { get; private set; }
    public double H { get; private set; }
    public double Score { get; private set; }

    public double Area => W * H;

    /// <summary>Intersection over Union — доля пересечения двух прямоугольников.</summary>
    public double IoU(BoundingBox other)
    {
        var ix = Math.Max(0, Math.Min(X + W, other.X + other.W) - Math.Max(X, other.X));
        var iy = Math.Max(0, Math.Min(Y + H, other.Y + other.H) - Math.Max(Y, other.Y));
        var intersection = ix * iy;
        var union = Area + other.Area - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
}
