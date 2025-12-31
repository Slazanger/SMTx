using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ReactiveUI;
using SMTx.Models;
using SMTx.Services;

namespace SMTx.ViewModels;

public class MainViewModel : ViewModelBase
{
    private ObservableCollection<RenderSolarSystem> _solarSystems = new();
    private double _offsetX;
    private double _offsetY;
    private double _scale = 1.0;

    public MainViewModel()
    {
        LoadSolarSystems();
    }

    public ObservableCollection<RenderSolarSystem> SolarSystems
    {
        get => _solarSystems;
        set => this.RaiseAndSetIfChanged(ref _solarSystems, value);
    }

    public double OffsetX
    {
        get => _offsetX;
        set => this.RaiseAndSetIfChanged(ref _offsetX, value);
    }

    public double OffsetY
    {
        get => _offsetY;
        set => this.RaiseAndSetIfChanged(ref _offsetY, value);
    }

    public double Scale
    {
        get => _scale;
        set => this.RaiseAndSetIfChanged(ref _scale, value);
    }

    private void LoadSolarSystems()
    {
        // Try to find the database path relative to workspace root
        // Start from the application directory and go up to find the workspace root
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var workspaceRoot = appDirectory;
        
        // Navigate up from bin/Debug/net8.0 to find the workspace root
        var directory = new DirectoryInfo(appDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "DataExport")))
        {
            directory = directory.Parent;
        }
        
        if (directory != null)
        {
            workspaceRoot = directory.FullName;
        }
        
        var dbPath = Path.Combine(workspaceRoot, "DataExport", "3142455", "render.db");
        
        if (!File.Exists(dbPath))
        {
            // Try alternative: relative to current directory
            var altPath = Path.Combine("DataExport", "3142455", "render.db");
            if (File.Exists(altPath))
            {
                dbPath = altPath;
            }
            else
            {
                // Log or handle the case where database is not found
                return;
            }
        }

        var reader = new RenderDatabaseReader(dbPath);
        var systems = reader.LoadSolarSystems();
        
        // Debug: Check if systems were loaded
        System.Diagnostics.Debug.WriteLine($"Loaded {systems.Count} solar systems from {dbPath}");
        if (systems.Count > 0)
        {
            var first = systems[0];
            System.Diagnostics.Debug.WriteLine($"First system: Id={first.Id}, Name={first.Name}, X={first.ScreenX}, Y={first.ScreenY}");
        }
        
        SolarSystems = new ObservableCollection<RenderSolarSystem>(systems);
        
        if (systems.Count > 0)
        {
            CalculateInitialView();
            System.Diagnostics.Debug.WriteLine($"Initial view: Scale={Scale}, OffsetX={OffsetX}, OffsetY={OffsetY}");
        }
    }

    private void CalculateInitialView()
    {
        if (_solarSystems.Count == 0)
            return;

        // Calculate bounding box
        var minX = _solarSystems.Min(s => s.ScreenX);
        var maxX = _solarSystems.Max(s => s.ScreenX);
        var minY = _solarSystems.Min(s => s.ScreenY);
        var maxY = _solarSystems.Max(s => s.ScreenY);

        System.Diagnostics.Debug.WriteLine($"Bounding box: X=[{minX}, {maxX}], Y=[{minY}, {maxY}]");

        var width = maxX - minX;
        var height = maxY - minY;

        // Assume canvas size (will be updated when canvas is sized)
        // For now, use a default size of 800x600
        var canvasWidth = 800.0;
        var canvasHeight = 600.0;

        double centerX;
        double centerY;

        // Handle case where all coordinates are the same or very close
        if (width <= 0.001 || height <= 0.001)
        {
            // If everything is at the same point, center it and use a reasonable scale
            centerX = minX;
            centerY = minY;
            Scale = 1.0;
            OffsetX = canvasWidth / 2.0 - centerX;
            OffsetY = canvasHeight / 2.0 - centerY;
            System.Diagnostics.Debug.WriteLine($"All coordinates clustered - using default view");
            return;
        }

        // Add 10% padding
        var padding = 0.1;
        var paddedWidth = width * (1 + 2 * padding);
        var paddedHeight = height * (1 + 2 * padding);

        // Calculate scale to fit
        var scaleX = canvasWidth / paddedWidth;
        var scaleY = canvasHeight / paddedHeight;
        Scale = Math.Min(scaleX, scaleY);

        // Center the view
        centerX = (minX + maxX) / 2.0;
        centerY = (minY + maxY) / 2.0;

        OffsetX = canvasWidth / 2.0 - centerX * Scale;
        OffsetY = canvasHeight / 2.0 - centerY * Scale;
    }

    public void UpdateCanvasSize(double width, double height)
    {
        if (_solarSystems.Count == 0)
            return;

        // Recalculate view with new canvas size
        var minX = _solarSystems.Min(s => s.ScreenX);
        var maxX = _solarSystems.Max(s => s.ScreenX);
        var minY = _solarSystems.Min(s => s.ScreenY);
        var maxY = _solarSystems.Max(s => s.ScreenY);

        var worldWidth = maxX - minX;
        var worldHeight = maxY - minY;

        double centerX;
        double centerY;

        // Handle case where all coordinates are the same or very close
        if (worldWidth <= 0.001 || worldHeight <= 0.001)
        {
            centerX = minX;
            centerY = minY;
            Scale = 1.0;
            OffsetX = width / 2.0 - centerX;
            OffsetY = height / 2.0 - centerY;
            return;
        }

        // Add 10% padding
        var padding = 0.1;
        var paddedWidth = worldWidth * (1 + 2 * padding);
        var paddedHeight = worldHeight * (1 + 2 * padding);

        // Calculate scale to fit
        var scaleX = width / paddedWidth;
        var scaleY = height / paddedHeight;
        Scale = Math.Min(scaleX, scaleY);

        // Center the view
        centerX = (minX + maxX) / 2.0;
        centerY = (minY + maxY) / 2.0;

        OffsetX = width / 2.0 - centerX * Scale;
        OffsetY = height / 2.0 - centerY * Scale;
    }
}
