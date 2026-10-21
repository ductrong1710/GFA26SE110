using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using NetTopologySuite.Geometries;

namespace FarmMonitoring.Application.Features.Farms;

public sealed record BoundaryPolygon(string Type, double[][][] Coordinates);

public static class BoundaryGeometry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static ValidationException Invalid(string message) =>
        new([new ValidationFailure("boundary", message)]);

    public static BoundaryPolygon? Normalize(BoundaryPolygon? value)
    {
        if (value is null) return null;
        if (value.Type != "Polygon" || value.Coordinates is not { Length: 1 } ||
            value.Coordinates[0] is not { Length: >= 3 } ring)
            throw Invalid("Boundary must be a Polygon with one exterior ring and at least three vertices.");
        if (ring.Any(p => p is not { Length: 2 } || !double.IsFinite(p[0]) || !double.IsFinite(p[1]) ||
            p[0] < -180 || p[0] > 180 || p[1] < -90 || p[1] > 90))
            throw Invalid("Boundary coordinates must contain valid longitude and latitude.");
        var vertices = ring.Select(p => new Coordinate(p[0], p[1])).ToList();
        if (vertices[0].Equals2D(vertices[^1])) vertices.RemoveAt(vertices.Count - 1);
        if (vertices.Count < 3 || vertices.Distinct().Count() != vertices.Count)
            throw Invalid("Boundary needs at least three distinct vertices, without repeated corners.");
        vertices.Add(vertices[0].Copy());
        var shape = new Polygon(new LinearRing(vertices.ToArray()));
        if (!shape.IsValid || shape.Area <= 0)
            throw Invalid("Boundary must have non-zero area and must not intersect itself.");
        return new("Polygon", [vertices.Select(p => new[] { p.X, p.Y }).ToArray()]);
    }

    // Same vertex centroid as Turf.centroid, excluding the closing coordinate.
    public static (decimal Latitude, decimal Longitude) Center(BoundaryPolygon value)
    {
        var ring = Normalize(value)!.Coordinates[0];
        var vertices = ring.Take(ring.Length - 1).ToArray();
        return ((decimal)vertices.Average(p => p[1]), (decimal)vertices.Average(p => p[0]));
    }

    public static void EnsureInside(BoundaryPolygon? child, BoundaryPolygon? parent)
    {
        if (child is null || parent is null) return;
        if (!Shape(parent).Covers(Shape(child)))
            throw Invalid("Zone boundary must be completely inside the farm boundary.");
    }

    private static Polygon Shape(BoundaryPolygon value) =>
        new(new LinearRing(Normalize(value)!.Coordinates[0].Select(p => new Coordinate(p[0], p[1])).ToArray()));

    public static string? Serialize(BoundaryPolygon? value) =>
        value is null ? null : JsonSerializer.Serialize(Normalize(value), JsonOptions);
    public static BoundaryPolygon? Deserialize(string? value) =>
        value is null ? null : JsonSerializer.Deserialize<BoundaryPolygon>(value, JsonOptions);
}
