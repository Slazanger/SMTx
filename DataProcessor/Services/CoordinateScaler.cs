using System.Text.Json;
using DataProcessor.Models;

namespace DataProcessor.Services;

public class CoordinateScaler
{
    private const double RenderRange = 10000.0;

    public class BoundingBox
    {
        public decimal MinX { get; set; }
        public decimal MaxX { get; set; }
        public decimal MinY { get; set; }
        public decimal MaxY { get; set; }
        public decimal MinZ { get; set; }
        public decimal MaxZ { get; set; }

        public decimal RangeX => MaxX - MinX;
        public decimal RangeY => MaxY - MinY;
        public decimal RangeZ => MaxZ - MinZ;
    }

    public BoundingBox CalculateBoundingBox(List<SolarSystem> systems)
    {
        if (systems == null || systems.Count == 0)
            throw new ArgumentException("Systems list cannot be empty");

        var validSystems = systems.Where(s => s.PositionX.HasValue && s.PositionY.HasValue && s.PositionZ.HasValue).ToList();
        
        if (validSystems.Count == 0)
            throw new ArgumentException("No systems with valid positions");

        return new BoundingBox
        {
            MinX = validSystems.Min(s => s.PositionX!.Value),
            MaxX = validSystems.Max(s => s.PositionX!.Value),
            MinY = validSystems.Min(s => s.PositionY!.Value),
            MaxY = validSystems.Max(s => s.PositionY!.Value),
            MinZ = validSystems.Min(s => s.PositionZ!.Value),
            MaxZ = validSystems.Max(s => s.PositionZ!.Value)
        };
    }

    public BoundingBox CalculateBoundingBox(List<Region> regions)
    {
        if (regions == null || regions.Count == 0)
            throw new ArgumentException("Regions list cannot be empty");

        var validRegions = regions.Where(r => r.PositionX.HasValue && r.PositionY.HasValue && r.PositionZ.HasValue).ToList();
        
        if (validRegions.Count == 0)
            throw new ArgumentException("No regions with valid positions");

        return new BoundingBox
        {
            MinX = validRegions.Min(r => r.PositionX!.Value),
            MaxX = validRegions.Max(r => r.PositionX!.Value),
            MinY = validRegions.Min(r => r.PositionY!.Value),
            MaxY = validRegions.Max(r => r.PositionY!.Value),
            MinZ = validRegions.Min(r => r.PositionZ!.Value),
            MaxZ = validRegions.Max(r => r.PositionZ!.Value)
        };
    }

    public BoundingBox CalculateBoundingBox(List<Constellation> constellations)
    {
        if (constellations == null || constellations.Count == 0)
            throw new ArgumentException("Constellations list cannot be empty");

        var validConstellations = constellations.Where(c => c.PositionX.HasValue && c.PositionY.HasValue && c.PositionZ.HasValue).ToList();
        
        if (validConstellations.Count == 0)
            throw new ArgumentException("No constellations with valid positions");

        return new BoundingBox
        {
            MinX = validConstellations.Min(c => c.PositionX!.Value),
            MaxX = validConstellations.Max(c => c.PositionX!.Value),
            MinY = validConstellations.Min(c => c.PositionY!.Value),
            MaxY = validConstellations.Max(c => c.PositionY!.Value),
            MinZ = validConstellations.Min(c => c.PositionZ!.Value),
            MaxZ = validConstellations.Max(c => c.PositionZ!.Value)
        };
    }

    public string ScaleCoordinates(decimal? x, decimal? y, decimal? z, BoundingBox bounds)
    {
        if (!x.HasValue || !y.HasValue || !z.HasValue)
        {
            return JsonSerializer.Serialize(new { x = 0.0, y = 0.0, z = 0.0 });
        }

        double scaledX = 0.0;
        double scaledY = 0.0;
        double scaledZ = 0.0;

        if (bounds.RangeX > 0)
        {
            scaledX = (double)((x.Value - bounds.MinX) / bounds.RangeX * (decimal)RenderRange);
        }

        if (bounds.RangeY > 0)
        {
            scaledY = (double)((y.Value - bounds.MinY) / bounds.RangeY * (decimal)RenderRange);
        }

        if (bounds.RangeZ > 0)
        {
            scaledZ = (double)((z.Value - bounds.MinZ) / bounds.RangeZ * (decimal)RenderRange);
        }

        return JsonSerializer.Serialize(new { x = scaledX, y = scaledY, z = scaledZ });
    }
}

