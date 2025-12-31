namespace SMTx.Models;

public class RenderSolarSystem
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public double ScreenX { get; set; }
    public double ScreenY { get; set; }
    public double WorldY { get; set; } // Depth coordinate (out of screen)
}

