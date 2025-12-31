using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SMTx.Models;
using SMTx.ViewModels;

namespace SMTx.Views;

public partial class MapCanvas : UserControl
{
    private MainViewModel? _viewModel;
    private Point _lastMousePosition;
    private bool _isDragging = false;
    private IPointer? _capturedPointer;
    
    // Performance optimization: Cache pens and typeface
    private static readonly Pen RegularLinkPen = new Pen(Brushes.Gray, 1.0);
    private static readonly Pen ConstellationLinkPen = new Pen(Brushes.Cyan, 1.0);
    private static readonly Pen RegionalLinkPen = new Pen(Brushes.Red, 1.0);
    private static readonly Typeface DefaultTypeface = new Typeface("Arial");
    
    // Throttle mouse updates to reduce redraw frequency
    private DateTime _lastMouseUpdate = DateTime.MinValue;
    private const int MouseUpdateThrottleMs = 16; // ~60 FPS max

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
            InvalidateVisual();
        }
    }

    private void OnSolarSystemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_viewModel != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _viewModel.UpdateCanvasSize(e.NewSize.Width, e.NewSize.Height);
        }
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
            
            // Throttle redraws to improve performance
            var now = DateTime.UtcNow;
            if ((now - _lastMouseUpdate).TotalMilliseconds >= MouseUpdateThrottleMs)
            {
                _lastMouseUpdate = now;
                InvalidateVisual();
            }
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
            
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // Fill background
        context.FillRectangle(Brushes.Black, bounds);

        if (_viewModel?.SolarSystems == null || _viewModel.SolarSystems.Count == 0)
        {
            // Draw a test circle in the center if no data
            var center = new Point(bounds.Width / 2.0, bounds.Height / 2.0);
            context.DrawEllipse(Brushes.Red, null, center, 20, 20);
            return;
        }

        var pixelSize = new PixelSize((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));

        // Project all 3D points to 2D using optimized batch method
        // This caches trigonometric values and is much faster
        var projectedResults = _viewModel.ProjectSystemsBatch(
            _viewModel.SolarSystems, 
            pixelSize.Width, 
            pixelSize.Height);
        
        var allProjected = projectedResults
            .Where(p => !double.IsNaN(p.screenX) && !double.IsNaN(p.screenY))
            .Select(p => new
            {
                System = p.system,
                ScreenX = p.screenX,
                ScreenY = p.screenY,
                Depth = p.depth
            })
            .ToList();

        var systemLookup = allProjected.ToDictionary(p => p.System.Id);

        // Draw stargate links
        if (_viewModel.StargateLinks != null && _viewModel.StargateLinks.Count > 0)
        {
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

                if (double.IsNaN(source.ScreenX) || double.IsNaN(dest.ScreenX))
                    continue;

                var sourceVisible = source.ScreenX >= minScreenX && source.ScreenX <= maxScreenX &&
                                   source.ScreenY >= minScreenY && source.ScreenY <= maxScreenY;
                var destVisible = dest.ScreenX >= minScreenX && dest.ScreenX <= maxScreenX &&
                                  dest.ScreenY >= minScreenY && dest.ScreenY <= maxScreenY;
                
                if (!sourceVisible && !destVisible)
                    continue;

                // Select pen based on link type (use cached pens for performance)
                Pen pen;
                switch (link.LinkType.ToLower())
                {
                    case "regional":
                        pen = RegionalLinkPen;
                        break;
                    case "constellation":
                        pen = ConstellationLinkPen;
                        break;
                    case "regular":
                    default:
                        pen = RegularLinkPen;
                        break;
                }

                context.DrawLine(pen, 
                    new Point(source.ScreenX, source.ScreenY),
                    new Point(dest.ScreenX, dest.ScreenY));
            }
        }

        // Filter visible systems and sort by depth
        var systemScreenMargin = 50.0;
        var projectedSystems = allProjected
            .Where(p => p.ScreenX >= -systemScreenMargin && p.ScreenX <= pixelSize.Width + systemScreenMargin &&
                       p.ScreenY >= -systemScreenMargin && p.ScreenY <= pixelSize.Height + systemScreenMargin)
            .OrderBy(p => p.Depth)
            .ToList();

        // Performance: Limit text rendering to closest systems only
        // Text rendering is expensive, so we only render labels for the closest N systems
        const int maxTextLabels = 150;
        var systemsWithText = projectedSystems
            .Where(p => !string.IsNullOrEmpty(p.System.Name))
            .Take(maxTextLabels)
            .ToHashSet();

        // Draw each solar system
        foreach (var projected in projectedSystems)
        {
            if (projected.ScreenX < 0 || projected.ScreenX > pixelSize.Width ||
                projected.ScreenY < 0 || projected.ScreenY > pixelSize.Height)
            {
                continue;
            }

            // Calculate size based on depth (closer = larger)
            var depthFactor = Math.Max(0.1, 1.0 - (projected.Depth + _viewModel.CameraDistance) / (_viewModel.CameraDistance * 2.0));
            var radius = 15.0 * depthFactor;

            var center = new Point(projected.ScreenX, projected.ScreenY);
            context.DrawEllipse(Brushes.White, null, center, radius, radius);
            
            // Draw system name below the circle (only if large enough to be readable)
            // Performance: Only render text for systems that are close enough (radius > 5)
            // and only render a limited number of text labels to avoid overload
            if (systemsWithText.Contains(projected) && radius > 5.0)
            {
                // Only render text for the closest systems to avoid performance issues
                // This is approximate - we're already sorted by depth, so we can limit count
                var fontSize = Math.Max(10.0, Math.Min(14.0, 14.0 * depthFactor));
                
                // Create FormattedText (unavoidable, but we limit when this happens)
                // Name is guaranteed non-null because systemsWithText only contains systems with names
                var formattedText = new FormattedText(
                    projected.System.Name!,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    DefaultTypeface,
                    fontSize,
                    Brushes.White);
                
                // Position text below the circle, centered horizontally
                var textX = center.X - formattedText.Width / 2.0;
                var textY = center.Y + radius + 5.0;
                var textPoint = new Point(textX, textY);
                
                context.DrawText(formattedText, textPoint);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
    }
}
