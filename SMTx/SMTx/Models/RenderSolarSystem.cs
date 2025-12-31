using System.Text.Json.Serialization;

namespace SMTx.Models;

public class RenderSolarSystem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    // 3D world coordinates
    [JsonPropertyName("worldX")]
    public double WorldX { get; set; }
    
    [JsonPropertyName("worldY")]
    public double WorldY { get; set; }
    
    [JsonPropertyName("worldZ")]
    public double WorldZ { get; set; }
    
    // 2D screen coordinates (calculated from 3D projection)
    [JsonPropertyName("screenX")]
    public double ScreenX { get; set; }
    
    [JsonPropertyName("screenY")]
    public double ScreenY { get; set; }
    
    [JsonPropertyName("depth")]
    public double Depth { get; set; } // For depth sorting
}

