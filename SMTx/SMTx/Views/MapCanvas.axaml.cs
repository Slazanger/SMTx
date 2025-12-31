using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using SMTx.Models;
using SMTx.ViewModels;

namespace SMTx.Views;

public partial class MapCanvas : UserControl
{
    private MainViewModel? _viewModel;
    private Bitmap? _bitmap;
    private bool _needsRedraw = true;
    private PixelSize _lastPixelSize;
    private Point _lastMousePosition;
    private bool _isDragging = false;
    private IPointer? _capturedPointer;

    public MapCanvas()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        
        // Mouse controls for 3D rotation
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            if (_viewModel.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged -= OnSolarSystemsCollectionChanged;
            }
        }

        _viewModel = DataContext as MainViewModel;
        
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            if (_viewModel.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged += OnSolarSystemsCollectionChanged;
            }
        }
        
        _needsRedraw = true;
        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SolarSystems))
        {
            if (_viewModel?.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged -= OnSolarSystemsCollectionChanged;
            }
            
            if (_viewModel?.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged += OnSolarSystemsCollectionChanged;
            }
        }
        
        if (e.PropertyName == nameof(MainViewModel.SolarSystems) ||
            e.PropertyName == nameof(MainViewModel.StargateLinks) ||
            e.PropertyName == nameof(MainViewModel.CameraDistance) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationX) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationY) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationZ) ||
            e.PropertyName == nameof(MainViewModel.FieldOfView))
        {
            _needsRedraw = true;
            InvalidateVisual();
        }
    }

    private void OnSolarSystemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _needsRedraw = true;
        InvalidateVisual();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_viewModel != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _viewModel.UpdateCanvasSize(e.NewSize.Width, e.NewSize.Height);
        }
        _needsRedraw = true;
        InvalidateVisual();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(this);
            _capturedPointer = e.Pointer;
            e.Pointer.Capture(this);
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging && _viewModel != null)
        {
            var currentPosition = e.GetPosition(this);
            var deltaX = currentPosition.X - _lastMousePosition.X;
            var deltaY = currentPosition.Y - _lastMousePosition.Y;

            // Rotate camera based on mouse movement
            _viewModel.CameraRotationY += deltaX * 0.01; // Horizontal rotation (yaw)
            _viewModel.CameraRotationX += deltaY * 0.01; // Vertical rotation (pitch)

            // Clamp pitch to avoid gimbal lock
            _viewModel.CameraRotationX = Math.Max(-Math.PI / 2.0 + 0.1, Math.Min(Math.PI / 2.0 - 0.1, _viewModel.CameraRotationX));

            _lastMousePosition = currentPosition;
            _needsRedraw = true;
            InvalidateVisual();
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging && _capturedPointer != null)
        {
            _isDragging = false;
            _capturedPointer.Capture(null);
            _capturedPointer = null;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel != null)
        {
            // Zoom in/out with mouse wheel
            var zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            _viewModel.CameraDistance *= zoomFactor;
            
            // Clamp distance
            _viewModel.CameraDistance = Math.Max(1000.0, Math.Min(100000.0, _viewModel.CameraDistance));
            
            _needsRedraw = true;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var pixelSize = new PixelSize((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));

        if (_needsRedraw || _bitmap == null || _lastPixelSize != pixelSize)
        {
            RedrawBitmap(pixelSize);
            _needsRedraw = false;
            _lastPixelSize = pixelSize;
        }

        if (_bitmap != null)
        {
            context.DrawImage(_bitmap, bounds);
        }
    }

    private void RedrawBitmap(PixelSize pixelSize)
    {
        var imageInfo = new SKImageInfo(pixelSize.Width, pixelSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        
        using var surface = SKSurface.Create(imageInfo);
        if (surface == null)
            return;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        if (_viewModel?.SolarSystems != null && _viewModel.SolarSystems.Count > 0)
        {
            // Project all 3D points to 2D and sort by depth
            var allProjected = _viewModel.SolarSystems
                .Select(system =>
                {
                    var (screenX, screenY, depth) = _viewModel.Project3DTo2D(
                        system.WorldX, system.WorldY, system.WorldZ,
                        pixelSize.Width, pixelSize.Height);
                    
                    return new
                    {
                        System = system,
                        ScreenX = screenX,
                        ScreenY = screenY,
                        Depth = depth
                    };
                })
                .Where(p => !double.IsNaN(p.ScreenX) && !double.IsNaN(p.ScreenY))
                .ToList();

            // Filter visible (wider range for debugging)
            var projectedSystems = allProjected
                .Where(p => p.ScreenX >= -pixelSize.Width * 2 && p.ScreenX <= pixelSize.Width * 3 &&
                           p.ScreenY >= -pixelSize.Height * 2 && p.ScreenY <= pixelSize.Height * 3)
                .OrderBy(p => p.Depth) // Sort by depth (back to front)
                .ToList();

            // Debug: Log first few projected points and all systems
            System.Diagnostics.Debug.WriteLine($"Total systems: {_viewModel.SolarSystems.Count}, Projected: {allProjected.Count}, Visible: {projectedSystems.Count}");
            if (allProjected.Count > 0)
            {
                var first = allProjected[0];
                System.Diagnostics.Debug.WriteLine($"First system: World({first.System.WorldX:F2}, {first.System.WorldY:F2}, {first.System.WorldZ:F2}) -> Screen({first.ScreenX:F2}, {first.ScreenY:F2}), Depth={first.Depth:F2}");
                System.Diagnostics.Debug.WriteLine($"Camera: Distance={_viewModel.CameraDistance:F2}, Center=({_viewModel.CameraCenterX:F2}, {_viewModel.CameraCenterY:F2}, {_viewModel.CameraCenterZ:F2})");
                System.Diagnostics.Debug.WriteLine($"Camera Rot: X={_viewModel.CameraRotationX:F2}, Y={_viewModel.CameraRotationY:F2}, Z={_viewModel.CameraRotationZ:F2}");
                
                // Show min/max screen coordinates
                if (allProjected.Count > 0)
                {
                    var minX = allProjected.Min(p => p.ScreenX);
                    var maxX = allProjected.Max(p => p.ScreenX);
                    var minY = allProjected.Min(p => p.ScreenY);
                    var maxY = allProjected.Max(p => p.ScreenY);
                    System.Diagnostics.Debug.WriteLine($"Screen bounds: X=[{minX:F2}, {maxX:F2}], Y=[{minY:F2}, {maxY:F2}], Canvas=[0, {pixelSize.Width}]x[0, {pixelSize.Height}]");
                }
            }

            // Create lookup dictionary for systems by ID
            var systemLookup = allProjected.ToDictionary(p => p.System.Id);

            // Draw stargate links first (behind systems)
            if (_viewModel.StargateLinks != null && _viewModel.StargateLinks.Count > 0)
            {
                foreach (var link in _viewModel.StargateLinks)
                {
                    if (!systemLookup.TryGetValue(link.SourceSystemId, out var source) ||
                        !systemLookup.TryGetValue(link.DestinationSystemId, out var dest))
                    {
                        continue;
                    }

                    // Skip if either point is off-screen
                    if (double.IsNaN(source.ScreenX) || double.IsNaN(dest.ScreenX))
                        continue;

                    // Determine color based on link type
                    SKColor linkColor;
                    float lineWidth = 1.0f;
                    
                    switch (link.LinkType.ToLower())
                    {
                        case "regional":
                            linkColor = SKColors.Red;
                            lineWidth = 2.0f;
                            break;
                        case "constellation":
                            linkColor = SKColors.Cyan;
                            lineWidth = 1.5f;
                            break;
                        case "regular":
                        default:
                            linkColor = SKColors.Gray;
                            lineWidth = 1.0f;
                            break;
                    }

                    // Create paint for the link
                    var linkPaint = new SKPaint
                    {
                        Color = linkColor,
                        IsAntialias = true,
                        Style = SKPaintStyle.Stroke,
                        StrokeWidth = lineWidth
                    };

                    // Draw line between systems
                    canvas.DrawLine(
                        (float)source.ScreenX, (float)source.ScreenY,
                        (float)dest.ScreenX, (float)dest.ScreenY,
                        linkPaint);
                }
            }

            // Create paint for solar systems
            var paint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            // Draw each solar system
            foreach (var projected in projectedSystems)
            {
                // Calculate size based on depth (closer = larger)
                var depthFactor = Math.Max(0.1, 1.0 - (projected.Depth + _viewModel.CameraDistance) / (_viewModel.CameraDistance * 2.0));
                var radius = (float)(5.0 * depthFactor);

                // Draw circle
                if (projected.ScreenX >= 0 && projected.ScreenX <= pixelSize.Width &&
                    projected.ScreenY >= 0 && projected.ScreenY <= pixelSize.Height)
                {
                    canvas.DrawCircle((float)projected.ScreenX, (float)projected.ScreenY, radius, paint);
                }
            }

            // Debug: Draw center point
            var centerPaint = new SKPaint
            {
                Color = SKColors.Red,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(pixelSize.Width / 2.0f, pixelSize.Height / 2.0f, 10.0f, centerPaint);
        }
        else
        {
            // Debug: Draw a test circle in the center if no data
            var testPaint = new SKPaint
            {
                Color = SKColors.Red,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(pixelSize.Width / 2.0f, pixelSize.Height / 2.0f, 20.0f, testPaint);
        }

        // Convert Skia image to Avalonia bitmap
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        if (data != null)
        {
            _bitmap?.Dispose();
            using var stream = data.AsStream();
            _bitmap = new Bitmap(stream);
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _bitmap?.Dispose();
        _bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }
}
