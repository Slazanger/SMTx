namespace DataProcessor.Models;

public class SolarSystem
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? ConstellationId { get; set; }
    public int? FactionId { get; set; }
    public decimal? PositionX { get; set; }
    public decimal? PositionY { get; set; }
    public decimal? PositionZ { get; set; }
    public decimal? Position2DX { get; set; }
    public decimal? Position2DY { get; set; }
    public decimal? Position2DZ { get; set; }
    public string? SecurityClass { get; set; }
    public decimal? SecurityStatus { get; set; }
}

