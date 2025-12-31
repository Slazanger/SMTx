using System;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using SMTx.Models;
using SMTx.ViewModels;

namespace SMTx.Views;

public partial class MapCanvas : UserControl
{
    private MainViewModel? _viewModel;
    private WriteableBitmap? _bitmap;
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

        // Only redraw if needed
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
        // Create or recreate WriteableBitmap
        if (_bitmap == null || _bitmap.PixelSize != pixelSize)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(pixelSize, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
        }

        var imageInfo = new SKImageInfo(pixelSize.Width, pixelSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        
        // Use WriteableBitmap's pixel buffer directly for GPU-accelerated rendering
        using (var lockedBitmap = _bitmap.Lock())
        {
            var address = lockedBitmap.Address;
            var rowBytes = lockedBitmap.RowBytes;
            
            using var surface = SKSurface.Create(imageInfo, address, rowBytes);
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

            // Filter visible systems (only those on or near screen)
            var systemScreenMargin = 50.0;
            var projectedSystems = allProjected
                .Where(p => p.ScreenX >= -systemScreenMargin && p.ScreenX <= pixelSize.Width + systemScreenMargin &&
                           p.ScreenY >= -systemScreenMargin && p.ScreenY <= pixelSize.Height + systemScreenMargin)
                .OrderBy(p => p.Depth) // Sort by depth (back to front)
                .ToList();

            // Debug output removed for performance

            // Create lookup dictionary for systems by ID
            var systemLookup = allProjected.ToDictionary(p => p.System.Id);

            // Draw stargate links first (behind systems)
            // Only draw links where both systems are visible on screen
            if (_viewModel.StargateLinks != null && _viewModel.StargateLinks.Count > 0)
            {
                // Pre-create paint objects for each link type to avoid recreating them
                var regionalPaint = new SKPaint
                {
                    Color = SKColors.Red,
                    IsAntialias = false, // Disable antialiasing for performance
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f
                };

                var constellationPaint = new SKPaint
                {
                    Color = SKColors.Cyan,
                    IsAntialias = false,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f
                };

                var regularPaint = new SKPaint
                {
                    Color = SKColors.Gray,
                    IsAntialias = false,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1.0f
                };

                // Calculate screen bounds with margin for lines that extend slightly off-screen
                var linkScreenMargin = 100.0;
                var minScreenX = -linkScreenMargin;
                var maxScreenX = pixelSize.Width + linkScreenMargin;
                var minScreenY = -linkScreenMargin;
                var maxScreenY = pixelSize.Height + linkScreenMargin;

                foreach (var link in _viewModel.StargateLinks)
                {
                    if (!systemLookup.TryGetValue(link.SourceSystemId, out var source) ||
                        !systemLookup.TryGetValue(link.DestinationSystemId, out var dest))
                    {
                        continue;
                    }

                    // Skip if either point is invalid
                    if (double.IsNaN(source.ScreenX) || double.IsNaN(dest.ScreenX))
                        continue;

                    // Quick visibility check - skip if both points are far off-screen
                    var sourceVisible = source.ScreenX >= minScreenX && source.ScreenX <= maxScreenX &&
                                       source.ScreenY >= minScreenY && source.ScreenY <= maxScreenY;
                    var destVisible = dest.ScreenX >= minScreenX && dest.ScreenX <= maxScreenX &&
                                      dest.ScreenY >= minScreenY && dest.ScreenY <= maxScreenY;
                    
                    // Only draw if at least one endpoint is visible
                    if (!sourceVisible && !destVisible)
                        continue;

                    // Select paint based on link type
                    SKPaint linkPaint;
                    switch (link.LinkType.ToLower())
                    {
                        case "regional":
                            linkPaint = regionalPaint;
                            break;
                        case "constellation":
                            linkPaint = constellationPaint;
                            break;
                        case "regular":
                        default:
                            linkPaint = regularPaint;
                            break;
                    }

                    // Draw line between systems
                    canvas.DrawLine(
                        (float)source.ScreenX, (float)source.ScreenY,
                        (float)dest.ScreenX, (float)dest.ScreenY,
                        linkPaint);
                }
            }

            // Create paint for solar systems (reuse single paint object)
            var paint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = false, // Disable antialiasing for performance
                Style = SKPaintStyle.Fill
            };

            // Draw each solar system (only visible ones)
            foreach (var projected in projectedSystems)
            {
                // Skip if off-screen
                if (projected.ScreenX < 0 || projected.ScreenX > pixelSize.Width ||
                    projected.ScreenY < 0 || projected.ScreenY > pixelSize.Height)
                {
                    continue;
                }

                // Calculate size based on depth (closer = larger)
                var depthFactor = Math.Max(0.1, 1.0 - (projected.Depth + _viewModel.CameraDistance) / (_viewModel.CameraDistance * 2.0));
                var radius = (float)(15.0 * depthFactor);

                // Draw circle
                canvas.DrawCircle((float)projected.ScreenX, (float)projected.ScreenY, radius, paint);
            }

        }
        else
        {
            // Debug: Draw a test circle in the center if no data
            var testPaint = new SKPaint
            {
                Color = SKColors.Red,
                IsAntialias = false,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawCircle(pixelSize.Width / 2.0f, pixelSize.Height / 2.0f, 20.0f, testPaint);
        }
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _bitmap?.Dispose();
        _bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }
}
