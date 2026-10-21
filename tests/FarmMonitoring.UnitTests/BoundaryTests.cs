using FarmMonitoring.Application.Features.Farms;
using FluentValidation;

namespace FarmMonitoring.UnitTests;

public class BoundaryTests
{
    private static BoundaryPolygon Box(double x, double y, double size) =>
        new("Polygon", [[[x, y], [x + size, y], [x + size, y + size], [x, y + size], [x, y]]]);

    [Fact]
    public void ClosedPolygonRoundTripsAndCalculatesCenter()
    {
        var boundary = Box(106, 10, 1);
        var normalized = BoundaryGeometry.Normalize(boundary)!;
        Assert.Equal(5, normalized.Coordinates[0].Length);
        Assert.Equal((10.5m, 106.5m), BoundaryGeometry.Center(normalized));
        Assert.Equal(BoundaryGeometry.Serialize(normalized),
            BoundaryGeometry.Serialize(BoundaryGeometry.Deserialize(BoundaryGeometry.Serialize(normalized))));
        Assert.Null(BoundaryGeometry.Normalize(null));
    }

    [Fact]
    public void InvalidGeometryIsRejected()
    {
        Assert.Throws<ValidationException>(() => BoundaryGeometry.Normalize(Box(181, 10, 1)));
        Assert.Throws<ValidationException>(() => BoundaryGeometry.Normalize(Box(10, 91, 1)));
        Assert.Throws<ValidationException>(() => BoundaryGeometry.Normalize(new("Polygon", [[[0, 0], [2, 2], [0, 2], [2, 0], [0, 0]]])));
        Assert.Throws<ValidationException>(() => BoundaryGeometry.Normalize(new("Polygon", [[[0, 0], [1, 0], [2, 0], [0, 0]]])));
    }

    [Fact]
    public void ContainmentChecksAllEdgesIncludingConcaveParents()
    {
        BoundaryGeometry.EnsureInside(Box(0.5, 0.5, 1), Box(0, 0, 4));
        BoundaryGeometry.EnsureInside(Box(0, 0, 4), Box(0, 0, 4));
        Assert.Throws<ValidationException>(() => BoundaryGeometry.EnsureInside(Box(3, 3, 2), Box(0, 0, 4)));
        var concave = new BoundaryPolygon("Polygon", [[[0, 0], [4, 0], [4, 4], [3, 4], [3, 1], [1, 1], [1, 4], [0, 4], [0, 0]]]);
        Assert.Throws<ValidationException>(() => BoundaryGeometry.EnsureInside(Box(0.5, 0.5, 3), concave));
    }
}
