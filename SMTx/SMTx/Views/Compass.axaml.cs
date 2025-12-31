using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using SMTx.ViewModels;

namespace SMTx.Views;

public partial class Compass : UserControl
{
    private MainViewModel? _viewModel;
    private WriteableBitmap? _bitmap;
    private bool _needsRedraw = true;

    public Compass()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        
        _needsRedraw = true;
        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CameraRotationX) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationY) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationZ))
        {
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

        if (_needsRedraw || _bitmap == null || _bitmap.PixelSize != pixelSize)
        {
            RedrawCompass(pixelSize);
            _needsRedraw = false;
        }

        if (_bitmap != null)
        {
            context.DrawImage(_bitmap, bounds);
        }
    }

    private void RedrawCompass(PixelSize pixelSize)
    {
        if (_bitmap == null || _bitmap.PixelSize != pixelSize)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(pixelSize, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
        }

        var imageInfo = new SKImageInfo(pixelSize.Width, pixelSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        
        using (var lockedBitmap = _bitmap.Lock())
        {
            var address = lockedBitmap.Address;
            var rowBytes = lockedBitmap.RowBytes;
            
            using var surface = SKSurface.Create(imageInfo, address, rowBytes);
            if (surface == null)
                return;

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            var centerX = pixelSize.Width / 2.0f;
            var centerY = pixelSize.Height / 2.0f;
            var radius = Math.Min(centerX, centerY) - 10;

            // Draw compass circle
            var circlePaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.0f
            };
            canvas.DrawCircle(centerX, centerY, radius, circlePaint);

            if (_viewModel != null)
            {
                // Draw cardinal directions
                var textPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true,
                    TextSize = 14,
                    TextAlign = SKTextAlign.Center
                };

                // North (Z+)
                var northAngle = -_viewModel.CameraRotationY;
                var northX = centerX + (float)(Math.Sin(northAngle) * (radius - 15));
                var northY = centerY - (float)(Math.Cos(northAngle) * (radius - 15));
                canvas.DrawText("N", northX, northY + 5, textPaint);

                // East (X+)
                var eastAngle = -_viewModel.CameraRotationY + Math.PI / 2.0;
                var eastX = centerX + (float)(Math.Sin(eastAngle) * (radius - 15));
                var eastY = centerY - (float)(Math.Cos(eastAngle) * (radius - 15));
                canvas.DrawText("E", eastX, eastY + 5, textPaint);

                // South (Z-)
                var southAngle = -_viewModel.CameraRotationY + Math.PI;
                var southX = centerX + (float)(Math.Sin(southAngle) * (radius - 15));
                var southY = centerY - (float)(Math.Cos(southAngle) * (radius - 15));
                canvas.DrawText("S", southX, southY + 5, textPaint);

                // West (X-)
                var westAngle = -_viewModel.CameraRotationY - Math.PI / 2.0;
                var westX = centerX + (float)(Math.Sin(westAngle) * (radius - 15));
                var westY = centerY - (float)(Math.Cos(westAngle) * (radius - 15));
                canvas.DrawText("W", westX, westY + 5, textPaint);

                // Draw direction indicator (arrow pointing in camera's forward direction)
                var indicatorPaint = new SKPaint
                {
                    Color = SKColors.Yellow,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };

                // Arrow pointing in the direction the camera is looking (based on rotation Y)
                var arrowAngle = -_viewModel.CameraRotationY;
                var arrowLength = radius - 5;
                var arrowX = centerX + (float)(Math.Sin(arrowAngle) * arrowLength);
                var arrowY = centerY - (float)(Math.Cos(arrowAngle) * arrowLength);

                // Draw arrow as a triangle
                var path = new SKPath();
                path.MoveTo(arrowX, arrowY);
                path.LineTo(
                    arrowX + (float)(Math.Sin(arrowAngle + 2.5) * 8),
                    arrowY - (float)(Math.Cos(arrowAngle + 2.5) * 8));
                path.LineTo(
                    arrowX + (float)(Math.Sin(arrowAngle - 2.5) * 8),
                    arrowY - (float)(Math.Cos(arrowAngle - 2.5) * 8));
                path.Close();
                canvas.DrawPath(path, indicatorPaint);
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

