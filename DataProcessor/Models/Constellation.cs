namespace DataProcessor.Models;

public class Constellation
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? RegionId { get; set; }
    public int? FactionId { get; set; }
    public decimal? PositionX { get; set; }
    public decimal? PositionY { get; set; }
    public decimal? PositionZ { get; set; }
}

