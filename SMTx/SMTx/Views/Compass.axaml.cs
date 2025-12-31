using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SMTx.ViewModels;

namespace SMTx.Views;

public partial class Compass : UserControl
{
    private MainViewModel? _viewModel;

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
        
        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CameraRotationX) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationY) ||
            e.PropertyName == nameof(MainViewModel.CameraRotationZ))
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var centerX = bounds.Width / 2.0;
        var centerY = bounds.Height / 2.0;
        var radius = Math.Min(centerX, centerY) - 10;
        var center = new Point(centerX, centerY);

        // Draw compass circle
        var pen = new Pen(Brushes.White, 2.0);
        context.DrawEllipse(null, pen, center, radius, radius);

        if (_viewModel != null)
        {
            // Draw cardinal directions
            var textBrush = Brushes.White;
            var typeface = new Typeface("Arial");
            var fontSize = 14.0;

            // North (Z+)
            var northAngle = -_viewModel.CameraRotationY;
            var northX = centerX + Math.Sin(northAngle) * (radius - 15);
            var northY = centerY - Math.Cos(northAngle) * (radius - 15);
            var northPoint = new Point(northX, northY);
            var northText = new FormattedText("N", System.Globalization.CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, typeface, fontSize, textBrush);
            context.DrawText(northText, northPoint);

            // East (X+)
            var eastAngle = -_viewModel.CameraRotationY + Math.PI / 2.0;
            var eastX = centerX + Math.Sin(eastAngle) * (radius - 15);
            var eastY = centerY - Math.Cos(eastAngle) * (radius - 15);
            var eastPoint = new Point(eastX, eastY);
            var eastText = new FormattedText("E", System.Globalization.CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, typeface, fontSize, textBrush);
            context.DrawText(eastText, eastPoint);

            // South (Z-)
            var southAngle = -_viewModel.CameraRotationY + Math.PI;
            var southX = centerX + Math.Sin(southAngle) * (radius - 15);
            var southY = centerY - Math.Cos(southAngle) * (radius - 15);
            var southPoint = new Point(southX, southY);
            var southText = new FormattedText("S", System.Globalization.CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, typeface, fontSize, textBrush);
            context.DrawText(southText, southPoint);

            // West (X-)
            var westAngle = -_viewModel.CameraRotationY - Math.PI / 2.0;
            var westX = centerX + Math.Sin(westAngle) * (radius - 15);
            var westY = centerY - Math.Cos(westAngle) * (radius - 15);
            var westPoint = new Point(westX, westY);
            var westText = new FormattedText("W", System.Globalization.CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, typeface, fontSize, textBrush);
            context.DrawText(westText, westPoint);

            // Draw direction indicator (arrow pointing in camera's forward direction)
            var arrowAngle = -_viewModel.CameraRotationY;
            var arrowLength = radius - 5;
            var arrowX = centerX + Math.Sin(arrowAngle) * arrowLength;
            var arrowY = centerY - Math.Cos(arrowAngle) * arrowLength;
            var arrowPoint = new Point(arrowX, arrowY);

            // Draw arrow as a triangle using StreamGeometry
            var arrowGeometry = new StreamGeometry();
            using (var ctx = arrowGeometry.Open())
            {
                ctx.BeginFigure(arrowPoint, true);
                ctx.LineTo(new Point(
                    arrowX + Math.Sin(arrowAngle + 2.5) * 8,
                    arrowY - Math.Cos(arrowAngle + 2.5) * 8));
                ctx.LineTo(new Point(
                    arrowX + Math.Sin(arrowAngle - 2.5) * 8,
                    arrowY - Math.Cos(arrowAngle - 2.5) * 8));
                ctx.EndFigure(true);
            }
            context.DrawGeometry(Brushes.Yellow, null, arrowGeometry);
        }
    }
}
