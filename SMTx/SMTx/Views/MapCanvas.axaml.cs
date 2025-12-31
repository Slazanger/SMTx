using System;
using System.Collections.Specialized;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using SMTx.ViewModels;

namespace SMTx.Views;

public partial class MapCanvas : UserControl
{
    private MainViewModel? _viewModel;
    private Bitmap? _bitmap;
    private bool _needsRedraw = true;
    private PixelSize _lastPixelSize;

    public MapCanvas()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from previous view model
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
            // Unsubscribe from old collection
            if (_viewModel?.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged -= OnSolarSystemsCollectionChanged;
            }
            
            // Subscribe to new collection
            if (_viewModel?.SolarSystems != null)
            {
                _viewModel.SolarSystems.CollectionChanged += OnSolarSystemsCollectionChanged;
            }
        }
        
        if (e.PropertyName == nameof(MainViewModel.SolarSystems) ||
            e.PropertyName == nameof(MainViewModel.OffsetX) ||
            e.PropertyName == nameof(MainViewModel.OffsetY) ||
            e.PropertyName == nameof(MainViewModel.Scale))
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

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var pixelSize = new PixelSize((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height));

        // Recreate bitmap if size changed or needs redraw
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

        // Clear canvas with black background
        canvas.Clear(SKColors.Black);

        if (_viewModel?.SolarSystems != null && _viewModel.SolarSystems.Count > 0)
        {
            // Create paint for solar systems
            var paint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            // Apply camera transform
            canvas.Save();
            canvas.Translate((float)_viewModel.OffsetX, (float)_viewModel.OffsetY);
            canvas.Scale((float)_viewModel.Scale);

            // Draw each solar system as a small circle
            // Use a fixed radius in world space (will be scaled by the camera transform)
            const float worldRadius = 10.0f; // 10 units in world space
            
            foreach (var system in _viewModel.SolarSystems)
            {
                var x = (float)system.ScreenX;
                var y = (float)system.ScreenY;
                
                // Draw circle with fixed world-space radius
                canvas.DrawCircle(x, y, worldRadius, paint);
            }

            canvas.Restore();
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
