using System;
using System.Collections.Generic;
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
    private List<StargateLink> _stargateLinks = new();
    
    // 3D Camera properties
    private double _cameraDistance = 20000.0;
    private double _cameraRotationX = 0.0; // Rotation around X axis (pitch) in radians
    private double _cameraRotationY = 0.0; // Rotation around Y axis (yaw) in radians
    private double _cameraRotationZ = 0.0; // Rotation around Z axis (roll) in radians
    private double _cameraCenterX = 0.0;
    private double _cameraCenterY = 0.0;
    private double _cameraCenterZ = 0.0;

    public double CameraCenterX => _cameraCenterX;
    public double CameraCenterY => _cameraCenterY;
    public double CameraCenterZ => _cameraCenterZ;
    private double _fieldOfView = Math.PI / 4.0; // 45 degrees

    public MainViewModel()
    {
        LoadSolarSystems();
    }

    public ObservableCollection<RenderSolarSystem> SolarSystems
    {
        get => _solarSystems;
        set => this.RaiseAndSetIfChanged(ref _solarSystems, value);
    }

    public List<StargateLink> StargateLinks
    {
        get => _stargateLinks;
        set
        {
            _stargateLinks = value;
            this.RaisePropertyChanged();
        }
    }

    // Camera distance (zoom)
    public double CameraDistance
    {
        get => _cameraDistance;
        set => this.RaiseAndSetIfChanged(ref _cameraDistance, value);
    }

    // Camera rotation angles in radians
    public double CameraRotationX
    {
        get => _cameraRotationX;
        set => this.RaiseAndSetIfChanged(ref _cameraRotationX, value);
    }

    public double CameraRotationY
    {
        get => _cameraRotationY;
        set => this.RaiseAndSetIfChanged(ref _cameraRotationY, value);
    }

    public double CameraRotationZ
    {
        get => _cameraRotationZ;
        set => this.RaiseAndSetIfChanged(ref _cameraRotationZ, value);
    }

    public double FieldOfView
    {
        get => _fieldOfView;
        set => this.RaiseAndSetIfChanged(ref _fieldOfView, value);
    }

    private void LoadSolarSystems()
    {
        // Try to find the database path relative to workspace root
        var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var workspaceRoot = appDirectory;
        
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
            var altPath = Path.Combine("DataExport", "3142455", "render.db");
            if (File.Exists(altPath))
            {
                dbPath = altPath;
            }
            else
            {
                return;
            }
        }

        var reader = new RenderDatabaseReader(dbPath);
        var systems = reader.LoadSolarSystems();
        var links = reader.LoadStargateLinks();
        
        System.Diagnostics.Debug.WriteLine($"Loaded {systems.Count} solar systems and {links.Count} stargate links from {dbPath}");
        
        SolarSystems = new ObservableCollection<RenderSolarSystem>(systems);
        StargateLinks = links;
        
        if (systems.Count > 0)
        {
            CalculateInitialCamera();
        }
    }

    private void CalculateInitialCamera()
    {
        if (_solarSystems.Count == 0)
            return;

        // Calculate bounding box in 3D
        var minX = _solarSystems.Min(s => s.WorldX);
        var maxX = _solarSystems.Max(s => s.WorldX);
        var minY = _solarSystems.Min(s => s.WorldY);
        var maxY = _solarSystems.Max(s => s.WorldY);
        var minZ = _solarSystems.Min(s => s.WorldZ);
        var maxZ = _solarSystems.Max(s => s.WorldZ);

        // Center of the universe
        _cameraCenterX = (minX + maxX) / 2.0;
        _cameraCenterY = (minY + maxY) / 2.0;
        _cameraCenterZ = (minZ + maxZ) / 2.0;

        // Calculate distance to fit everything in view
        var width = maxX - minX;
        var height = maxY - minY;
        var depth = maxZ - minZ;
        var maxDimension = Math.Max(Math.Max(width, height), depth);
        
        // Set camera distance to show everything with some padding
        // Use a reasonable distance that will work with the projection
        CameraDistance = maxDimension * 2.0;
        
        System.Diagnostics.Debug.WriteLine($"Bounding box: X=[{minX:F2}, {maxX:F2}], Y=[{minY:F2}, {maxY:F2}], Z=[{minZ:F2}, {maxZ:F2}]");
        System.Diagnostics.Debug.WriteLine($"Max dimension: {maxDimension:F2}, Camera distance: {CameraDistance:F2}");

        // Start with a nice isometric view (45 degrees rotation)
        CameraRotationX = Math.PI / 6.0; // 30 degrees pitch
        CameraRotationY = Math.PI / 4.0; // 45 degrees yaw
        CameraRotationZ = 0.0;
    }

    public void UpdateCanvasSize(double width, double height)
    {
        // Canvas size is used in the projection calculation
        // This method can be used to adjust FOV if needed
    }

    // Helper method to project 3D point to 2D screen coordinates
    public (double screenX, double screenY, double depth) Project3DTo2D(double worldX, double worldY, double worldZ, double canvasWidth, double canvasHeight)
    {
        // Translate to camera center (center of universe)
        var x = worldX - _cameraCenterX;
        var y = worldY - _cameraCenterY;
        var z = worldZ - _cameraCenterZ;

        // Apply rotations (in order: Z, Y, X) - these rotate the world, not the camera
        // Rotation around Z axis
        var cosZ = Math.Cos(-_cameraRotationZ);
        var sinZ = Math.Sin(-_cameraRotationZ);
        var tempX = x * cosZ - y * sinZ;
        var tempY = x * sinZ + y * cosZ;
        x = tempX;
        y = tempY;

        // Rotation around Y axis
        var cosY = Math.Cos(-_cameraRotationY);
        var sinY = Math.Sin(-_cameraRotationY);
        tempX = x * cosY + z * sinY;
        var tempZ = -x * sinY + z * cosY;
        x = tempX;
        z = tempZ;

        // Rotation around X axis
        var cosX = Math.Cos(-_cameraRotationX);
        var sinX = Math.Sin(-_cameraRotationX);
        tempY = y * cosX - z * sinX;
        tempZ = y * sinX + z * cosX;
        y = tempY;
        z = tempZ;

        // Translate camera back along Z axis (camera is at distance from center)
        z = z + _cameraDistance;

        // Perspective projection
        if (z <= 0.1) // Behind camera, don't render
        {
            return (double.NaN, double.NaN, double.NaN);
        }

        // Perspective divide
        var perspectiveScale = _cameraDistance / z;
        var projectedX = x * perspectiveScale;
        var projectedY = y * perspectiveScale;
        var depth = z; // Store depth for sorting

        // Scale to canvas size
        // The projected coordinates are in world space at the camera plane
        // We need to scale them to fit on the canvas
        // Use a scale based on the camera distance to show a reasonable view
        
        // Calculate the world size we want to show (based on camera distance)
        // At distance d, we want to show roughly d units of world space
        var worldViewSize = _cameraDistance;
        
        // Scale to fit on canvas (use the smaller dimension to ensure everything fits)
        var scale = Math.Min(canvasWidth, canvasHeight) / worldViewSize;
        
        // Convert to screen coordinates (center of screen is origin)
        var screenX = projectedX * scale + canvasWidth / 2.0;
        var screenY = projectedY * scale + canvasHeight / 2.0;

        return (screenX, screenY, depth);
    }
}
