namespace SMTx.Models;

public class StargateLink
{
    public int SourceSystemId { get; set; }
    public int DestinationSystemId { get; set; }
    public string LinkType { get; set; } = "regular";
}

