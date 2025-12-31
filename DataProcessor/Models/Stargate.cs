namespace DataProcessor.Models;

public class Stargate
{
    public int Id { get; set; }
    public int SourceSystemId { get; set; }
    public int DestinationSystemId { get; set; }
    public int? DestinationStargateId { get; set; }
}

