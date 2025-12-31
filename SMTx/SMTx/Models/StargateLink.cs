using System.Text.Json.Serialization;

namespace SMTx.Models;

public class StargateLink
{
    [JsonPropertyName("sourceSystemId")]
    public int SourceSystemId { get; set; }
    
    [JsonPropertyName("destinationSystemId")]
    public int DestinationSystemId { get; set; }
    
    [JsonPropertyName("linkType")]
    public string LinkType { get; set; } = "regular";
}

