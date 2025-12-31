namespace SMTx.Models;

public class RenderSolarSystem
{
    public int Id { get; set; }
    public string? Name { get; set; }
    
    // 3D world coordinates
    public double WorldX { get; set; }
    public double WorldY { get; set; }
    public double WorldZ { get; set; }
    
    // 2D screen coordinates (calculated from 3D projection)
    public double ScreenX { get; set; }
    public double ScreenY { get; set; }
    public double Depth { get; set; } // For depth sorting
}

