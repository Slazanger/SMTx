using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SMTx.Views;

internal static class MapHostSetup
{
    public static void AttachMapAndOverlays(Grid mapContainer)
    {
        if (mapContainer.Children.OfType<MapCanvas>().Any() ||
            mapContainer.Children.OfType<WebGLMapCanvas>().Any())
            return;

        if (OperatingSystem.IsBrowser())
        {
            var fallbackCanvas = new MapCanvas { IsVisible = false };
            mapContainer.Children.Add(fallbackCanvas);
            mapContainer.Children.Add(new WebGLMapCanvas());
        }
        else
        {
            mapContainer.Children.Add(new MapCanvas());
        }

        var zoomControlBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5)
        };
        zoomControlBorder.Child = new ZoomControl();
        mapContainer.Children.Add(zoomControlBorder);

        var compassBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5)
        };
        compassBorder.Child = new Compass { Width = 120, Height = 120 };
        mapContainer.Children.Add(compassBorder);

        var directionalPadBorder = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(10),
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5)
        };
        directionalPadBorder.Child = new DirectionalPad();
        mapContainer.Children.Add(directionalPadBorder);
    }
}
